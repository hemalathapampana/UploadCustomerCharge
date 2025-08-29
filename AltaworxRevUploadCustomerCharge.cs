using Altaworx.AWS.Core;
using Altaworx.AWS.Core.Models;
using Altaworx.AWS.Core.Repositories.CustomerCharge;
using Altaworx.AWS.Core.Repositories.OptimizationInstance;
using Altaworx.AWS.Core.Repositories.OptimizationQueue;
using Altaworx.AWS.Core.Services.SQS;
using AltaworxRevAWSCreateCustomerChange.Services.ChargeList;
using AltaworxSFTPUploadCustomerChargeLambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amop.Core.Constants;
using Amop.Core.Models.Settings;
using Amop.Core.Repositories.Environment;
using Amop.Core.Services.Base64Service;
using static Altaworx.AWS.Core.RevIOCommon;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AltaworxRevUploadCustomerCharge;

public class Function : AwsFunctionBase
{
    private string CustomerChargesS3BucketName;
    private string ProcessUploadedCustomerChargeQueueURL = string.Empty;
    private SettingsRepository settingsRepository;
    private IS3Wrapper s3Wrapper;
    private CustomerChargeRepository customerChargeRepository;
    private OptimizationQueueRepository optimizationQueueRepository;
    private CustomerChargeListFileService customerChargeListFileService;
    private OptimizationInstanceRepository optimizationInstanceRepository;
    protected SqsService sqsService = new SqsService();
    private readonly EnvironmentRepository environmentRepo = new EnvironmentRepository();

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        AmopLambdaContext lambdaContext = null;
        try
        {
            lambdaContext = base.BaseAmopFunctionHandler(context);
            TryGetAllEnvironmentVariables(lambdaContext);
            InitializeRepositories(lambdaContext);
            InitializeServices();
            await ProcessEvent(lambdaContext, sqsEvent);
        }
        catch (Exception ex)
        {
            LogInfo(lambdaContext, CommonConstants.SUB, $"{ex.Message} - {ex.StackTrace}");
        }
        base.CleanUp(lambdaContext);
    }

    private void InitializeRepositories(AmopLambdaContext context)
    {
        var base64Service = new Base64Service();
        customerChargeRepository = new CustomerChargeRepository(context.CentralDbConnectionString);
        settingsRepository = new SettingsRepository(context.logger, context.CentralDbConnectionString, base64Service);
        optimizationQueueRepository = new OptimizationQueueRepository(context.logger, context.CentralDbConnectionString);
        optimizationInstanceRepository = new OptimizationInstanceRepository(context.logger, context.CentralDbConnectionString);
    }

    private void InitializeServices()
    {
        var generalProviderSettings = settingsRepository.GetGeneralProviderSettings();
        s3Wrapper = new S3Wrapper(generalProviderSettings.AwsCredentials, CustomerChargesS3BucketName);
        customerChargeListFileService = new CustomerChargeListFileService();
    }

    private void TryGetAllEnvironmentVariables(AmopLambdaContext lambdaContext)
    {
        ProcessUploadedCustomerChargeQueueURL = GetStringValueFromEnvironmentVariable(lambdaContext.Context, environmentRepo, EnvironmentVariableKeyConstants.PROCESS_UPLOADED_CUSTOMER_CHARGE_QUEUE_URL);
        CustomerChargesS3BucketName = GetStringValueFromEnvironmentVariable(lambdaContext.Context, environmentRepo, EnvironmentVariableKeyConstants.CUSTOMER_CHARGES_S3_BUCKET_NAME);
    }

    private async Task ProcessEvent(AmopLambdaContext context, SQSEvent sqsEvent)
    {
        LogInfo(context, CommonConstants.SUB);
        if (sqsEvent?.Records?.Count > 0)
        {
            LogInfo(context, CommonConstants.INFO, string.Format(LogCommonStrings.BEGINNING_PROCESS, sqsEvent.Records.Count));
            foreach (var record in sqsEvent.Records)
            {
                LogInfo(context, CommonConstants.INFO, $"{nameof(record.MessageId)}: {record.MessageId}");
                var sqsValues = new SqsValues(context, record);

                var queue = optimizationQueueRepository.GetQueue(sqsValues.QueueId);
                var instance = optimizationInstanceRepository.GetInstance(queue.InstanceId);
                context.LoadOptimizationSettingsByTenantId(instance.TenantId);

                await ProcessQueueAsync(context, sqsValues.QueueId, instance, sqsValues);
            }
        }
    }

    public async Task ProcessQueueAsync(AmopLambdaContext context, long queueId, OptimizationInstance instance, SqsValues sqsValues)
    {
        LogInfo(context, CommonConstants.SUB, $"({queueId},{instance.Id})");
        LogVariableValue(context, nameof(context.OptimizationSettings.OptIntoCrossProviderCustomerOptimization), context.OptimizationSettings.OptIntoCrossProviderCustomerOptimization);

        var serviceProviderList = ServiceProviderCommon.GetServiceProviders(context.CentralDbConnectionString);
        var deviceList = customerChargeRepository.GetDeviceCustomerChargeList(context, ParameterizedLog(context), queueId, sqsValues.PortalTypeId);
        if (deviceList?.Count() == 0)
        {
            LogInfo(context, CommonConstants.INFO, string.Format(LogCommonStrings.ALL_ITEMS_OF_QUEUE_HAS_BEEN_PROCESSED, queueId));
        }
        else
        {
            await ProcessCustomerCharge(context, queueId, instance, sqsValues, deviceList, serviceProviderList);
        }
    }

    private async Task ProcessCustomerCharge(AmopLambdaContext context, long queueId, OptimizationInstance instance, SqsValues sqsValues, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList, List<ServiceProvider> serviceProviders)
    {
        LogInfo(context, CommonConstants.SUB, $"({queueId},{instance.Id})");
        ProcessDeviceList(context, sqsValues, deviceList);
        var isUploadFileSuccessfully = await UploadDeviceListToS3(context, queueId, instance, deviceList, serviceProviders);
        LogInfo(context, CommonConstants.INFO, $"{nameof(isUploadFileSuccessfully)}:{isUploadFileSuccessfully}");

        await CheckUploadCustomerChargeProgress(context, sqsValues.QueueId, instance.TenantId, sqsValues.PortalTypeId, sqsValues.InstanceIds, isUploadFileSuccessfully);
    }

    private async Task<bool> UploadDeviceListToS3(AmopLambdaContext context, long queueId, OptimizationInstance instance, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList, List<ServiceProvider> serviceProviders)
    {
        LogInfo(context, CommonConstants.SUB, $"({queueId},{instance.Id})");
        var fileName = $"{queueId}.txt";
        var chargeListFileBytes = customerChargeListFileService.GenerateChargeListFile(deviceList, instance.BillingPeriodStartDate,
            instance.BillingPeriodEndDate, serviceProviders);
        s3Wrapper.UploadAwsFile(chargeListFileBytes, fileName);
        var statusUploadFileToS3 = await s3Wrapper.WaitForFileUploadCompletion(fileName, CommonConstants.DELAY_IN_SECONDS_FIVE_MINUTES, context.logger);
        var statusUploadFile = (IsUploadSuccess: statusUploadFileToS3.Item1, ErrorMessage: statusUploadFileToS3.Item2);
        if (!statusUploadFile.IsUploadSuccess)
        {
            LogInfo(context, CommonConstants.ERROR, string.Format(LogCommonStrings.UPLOAD_FILE_TO_S3_NOT_SUCCESS, $"{queueId}.txt") + " " + statusUploadFile.ErrorMessage);
            return false;
        }
        return true;
    }

    private void ProcessDeviceList(AmopLambdaContext context, SqsValues sqsValues, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList)
    {
        LogInfo(context, CommonConstants.SUB, $"{sqsValues.QueueId}");
        foreach (var device in deviceList)
        {
            customerChargeRepository.MarkRecordProcessed(ParameterizedLog(context), sqsValues.PortalTypeId, device, -1);
        }
    }

    private async Task CheckUploadCustomerChargeProgress(AmopLambdaContext context, long queueId, int tenantId, int portalTypeId, string instanceIds, bool isSuccess)
    {
        var attributes = new Dictionary<string, string>()
        {
            {SQSMessageKeyConstant.QUEUE_ID, queueId.ToString()},
            {SQSMessageKeyConstant.TENANT_ID, tenantId.ToString()},
            {SQSMessageKeyConstant.PORTAL_TYPE_ID, portalTypeId.ToString()},
            {SQSMessageKeyConstant.INSTANCE_IDS, instanceIds},
            {SQSMessageKeyConstant.IS_SUCCESSFUL, isSuccess.ToString()},
        };

        await sqsService.SendSQSMessage(ParameterizedLog(context), AwsCredentials(context), ProcessUploadedCustomerChargeQueueURL, attributes);
    }
}
