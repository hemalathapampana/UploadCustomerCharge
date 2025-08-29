# AltaworxRevUploadCustomerCharge Lambda Function - Sequential Flow Analysis

## Overview
This document provides a comprehensive high-level sequential flow analysis of the `AltaworxRevUploadCustomerCharge` lambda function, showing the complete method call chain from start to finish.

---

## Main Entry Point

### 1. `FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:35`
- **Purpose**: Main AWS Lambda entry point that processes SQS events
- **Flow**: Initializes context, repositories, services, and processes events

---

## Initialization Phase

### 2. `BaseAmopFunctionHandler(ILambdaContext context, bool skipOUSpecificLogic = false)`
**Location**: `AwsFunctionBase.cs:47`
- **Purpose**: Creates and returns AmopLambdaContext
- **Calls**: `AmopLambdaContext` constructor

### 3. `AmopLambdaContext(ILambdaContext context, bool skipOUSpecificLogic)`
**Location**: `KeySysLambdaContext.cs` (inherited by AmopLambdaContext)
- **Purpose**: Initializes lambda context with database connections and settings
- **Calls**: `InitializeContext(skipOUSpecificLogic)`

### 4. `InitializeContext(bool skipOUSpecificLogic)`
**Location**: `KeySysLambdaContext.cs:74`
- **Purpose**: Sets up environment repository, logger, connection strings
- **Calls**: 
  - `EnvironmentRepository()` constructor
  - `KeysysLambdaLogger()` constructor
  - `Base64Service()` constructor
  - `SettingsRepository()` constructor
  - `TenantRepository()` constructor
  - `LoadOUSettings()`

### 5. `LoadOUSettings()`
**Location**: `KeySysLambdaContext.cs:113`
- **Purpose**: Loads optimization and general provider settings
- **Calls**:
  - `LoadOptimizationSettings(settingsRepository)`
  - `GetGeneralProviderSettings()`

### 6. `GetOptimizationSettings(int? tenantId = null)`
**Location**: `SettingsRepository.cs:109`
- **Purpose**: Retrieves optimization settings from database
- **Calls**:
  - `SqlQueryHelper.ExecuteStoredProcedureWithListResult()`
  - `MapToOptimizationSettingsModel(settingsValues)`

### 7. `GetGeneralProviderSettings()`
**Location**: `SettingsRepository.cs:32`
- **Purpose**: Retrieves general provider settings (AWS credentials, email settings, etc.)
- **Calls**: Database query execution

### 8. `TryGetAllEnvironmentVariables(AmopLambdaContext lambdaContext)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:69`
- **Purpose**: Retrieves environment variables for queue URL and S3 bucket
- **Calls**: 
  - `GetStringValueFromEnvironmentVariable()` (twice)

### 9. `GetStringValueFromEnvironmentVariable(ILambdaContext context, EnvironmentRepository environmentRepo, string key)`
**Location**: `AwsFunctionBase.cs:358`
- **Purpose**: Gets environment variable value with validation
- **Calls**: `environmentRepo.GetEnvironmentVariable()`

### 10. `InitializeRepositories(AmopLambdaContext context)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:53`
- **Purpose**: Creates repository instances
- **Calls**:
  - `Base64Service()` constructor
  - `CustomerChargeRepository()` constructor
  - `SettingsRepository()` constructor
  - `OptimizationQueueRepository()` constructor
  - `OptimizationInstanceRepository()` constructor

### 11. `InitializeServices()`
**Location**: `AltaworxRevUploadCustomerCharge.cs:62`
- **Purpose**: Creates service instances
- **Calls**:
  - `settingsRepository.GetGeneralProviderSettings()`
  - `S3Wrapper()` constructor
  - `CustomerChargeListFileService()` constructor

---

## Event Processing Phase

### 12. `ProcessEvent(AmopLambdaContext context, SQSEvent sqsEvent)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:75`
- **Purpose**: Processes each SQS message record
- **Calls**: `ProcessQueueAsync()` for each record

### 13. `SqsValues(AmopLambdaContext context, SQSEvent.SQSMessage record)`
**Location**: `SqsValues.cs:20`
- **Purpose**: Parses SQS message attributes (QueueId, PortalTypeId, InstanceIds)

### 14. `GetQueue(long queueId)`
**Location**: `OptimizationQueueRepository.cs:19`
- **Purpose**: Retrieves queue information from database
- **Calls**: `QueueFromReader(rdr)`

### 15. `GetInstance(long instanceId)`
**Location**: `OptimizationInstanceRepository.cs:21`
- **Purpose**: Retrieves optimization instance information
- **Calls**: `InstanceFromReader(optimizationInstanceDataReader)`

### 16. `LoadOptimizationSettingsByTenantId(int tenantId)`
**Location**: `KeySysLambdaContext.cs:192`
- **Purpose**: Loads tenant-specific optimization settings
- **Calls**: `SettingsRepo.GetOptimizationSettings(tenantId)`

---

## Queue Processing Phase

### 17. `ProcessQueueAsync(AmopLambdaContext context, long queueId, OptimizationInstance instance, SqsValues sqsValues)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:95`
- **Purpose**: Main queue processing logic
- **Calls**:
  - `ServiceProviderCommon.GetServiceProviders()`
  - `customerChargeRepository.GetDeviceCustomerChargeList()`
  - `ProcessCustomerCharge()` (if device list exists)

### 18. `GetServiceProviders(string connectionString)`
**Location**: `ServiceProviderCommon.cs:83`
- **Purpose**: Retrieves all service providers from database
- **Returns**: List of ServiceProvider objects

### 19. `GetDeviceCustomerChargeList(AmopLambdaContext context, Action<string, string> logFunction, long queueId, int portalTypeId)`
**Location**: `CustomerChargeRepository.cs:70`
- **Purpose**: Retrieves device customer charge records for processing
- **Calls**:
  - `SqlQueryHelper.ExecuteStoredProcedureWithListResult()` 
  - Uses different stored procedures based on `OptIntoCrossProviderCustomerOptimization` setting
  - `ReadDeviceRecordFromReader()`

---

## Customer Charge Processing Phase

### 20. `ProcessCustomerCharge(AmopLambdaContext context, long queueId, OptimizationInstance instance, SqsValues sqsValues, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList, List<ServiceProvider> serviceProviders)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:112`
- **Purpose**: Processes customer charges and uploads to S3
- **Calls**:
  - `ProcessDeviceList()`
  - `UploadDeviceListToS3()`
  - `CheckUploadCustomerChargeProgress()`

### 21. `ProcessDeviceList(AmopLambdaContext context, SqsValues sqsValues, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:139`
- **Purpose**: Marks each device record as processed
- **Calls**: `customerChargeRepository.MarkRecordProcessed()` for each device

### 22. `MarkRecordProcessed(Action<string, string> logFunction, int portalTypeId, DeviceCustomerChargeQueueRecord device, int chargeId)`
**Location**: `CustomerChargeRepository.cs:188`
- **Purpose**: Updates device customer charge queue record in database
- **Calls**: `SqlQueryHelper.ExecuteStoredProcedureWithIntResult()`

---

## S3 Upload Phase

### 23. `UploadDeviceListToS3(AmopLambdaContext context, long queueId, OptimizationInstance instance, IEnumerable<DeviceCustomerChargeQueueRecord> deviceList, List<ServiceProvider> serviceProviders)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:122`
- **Purpose**: Generates charge list file and uploads to S3
- **Calls**:
  - `customerChargeListFileService.GenerateChargeListFile()`
  - `s3Wrapper.UploadAwsFile()`
  - `s3Wrapper.WaitForFileUploadCompletion()`

### 24. `GenerateChargeListFile(IEnumerable<DeviceCustomerChargeQueueRecord> chargeList, DateTime billingPeriodStartDate, DateTime billingPeriodEndDate, List<ServiceProvider> serviceProviders)`
**Location**: `CustomerChargeListFileService.cs:12`
- **Purpose**: Creates tab-separated file with charge data
- **Calls**:
  - `WriteChargeListFileHeader()`
  - `WriteChargeListFileBody()`

### 25. `WriteChargeListFileHeader(TextWriter sw)`
**Location**: `CustomerChargeListFileService.cs:53`
- **Purpose**: Writes CSV/TSV header row

### 26. `WriteChargeListFileBody(TextWriter sw, ICollection<DeviceCustomerChargeQueueRecord> chargeList, DateTime billingPeriodStart, DateTime billingPeriodEnd, List<ServiceProvider> serviceProviders)`
**Location**: `CustomerChargeListFileService.cs:59`
- **Purpose**: Writes charge data rows and footer
- **Calls**:
  - `RevIOHelper.BuildBillingPeriodDay()` for each charge
  - `WriteChargeRow()` for each charge
  - `WriteChargeListFileFooter()`

### 27. `WriteChargeRow(TextWriter sw, DeviceCustomerChargeQueueRecord charge, string billingPeriodStart, string billingPeriodEnd)`
**Location**: `CustomerChargeListFileService.cs:94`
- **Purpose**: Writes individual charge record row

### 28. `WriteChargeListFileFooter(TextWriter sw, IEnumerable<DeviceCustomerChargeQueueRecord> chargeList)`
**Location**: `CustomerChargeListFileService.cs:114`
- **Purpose**: Writes summary row with total charges

### 29. `UploadAwsFile(byte[] fileBytes, string awsFileName)`
**Location**: `S3Wrapper.cs:138`
- **Purpose**: Uploads file bytes to S3
- **Calls**: `UploadAwsFile(Stream s, string awsFileName)`

### 30. `UploadAwsFile(Stream s, string awsFileName)`
**Location**: `S3Wrapper.cs:146`
- **Purpose**: Uploads stream to S3 using AWS SDK
- **Calls**: `_s3Client.PutObjectAsync(request)`

### 31. `WaitForFileUploadCompletion(string key, int timeoutInSeconds, IKeysysLogger logger = null)`
**Location**: `S3Wrapper.cs:269`
- **Purpose**: Waits for S3 upload to complete by checking file metadata
- **Calls**: `_s3Client.GetObjectMetadataAsync()` in a loop

---

## Final Notification Phase

### 32. `CheckUploadCustomerChargeProgress(AmopLambdaContext context, long queueId, int tenantId, int portalTypeId, string instanceIds, bool isSuccess)`
**Location**: `AltaworxRevUploadCustomerCharge.cs:148`
- **Purpose**: Sends SQS message with upload results
- **Calls**: `sqsService.SendSQSMessage()`

### 33. `SendSQSMessage(Action<string, string> logFunction, BasicAWSCredentials awsCredentials, string destinationQueueUrl, Dictionary<string, string> attributeDictionary = null, int delaySeconds = 0)`
**Location**: `SqsService.cs:17`
- **Purpose**: Sends message to SQS queue with upload status
- **Calls**: 
  - `AmazonSQSClient()` constructor
  - `client.SendMessageAsync(request)` with retry policy

---

## Cleanup Phase

### 34. `CleanUp(AmopLambdaContext lambdaContext)`
**Location**: `AwsFunctionBase.cs:53`
- **Purpose**: Performs cleanup operations
- **Calls**: `context.CleanUp()`

### 35. `CleanUp()`
**Location**: `KeySysLambdaContext.cs:139`
- **Purpose**: Flushes logger
- **Calls**: `logger.Flush()`

---

## Key Helper Methods Used Throughout

### Database Operations
- **SqlQueryHelper.ExecuteStoredProcedureWithListResult()** - Executes stored procedures returning lists
- **SqlQueryHelper.ExecuteStoredProcedureWithIntResult()** - Executes stored procedures returning integers
- **Various FromReader() methods** - Parse database records into objects

### Logging
- **LogInfo()** - Logs information throughout the process
- **ParameterizedLog()** - Creates parameterized logging function

### AWS Operations
- **AwsCredentials()** - Gets AWS credentials for S3 and SQS operations

---

## Summary

The lambda function follows this high-level flow:

1. **Initialization**: Set up context, repositories, and services
2. **Event Processing**: Parse SQS messages and retrieve queue/instance data  
3. **Data Retrieval**: Get service providers and device charge records
4. **File Generation**: Create charge list file with billing data
5. **S3 Upload**: Upload file to S3 and wait for completion
6. **Record Processing**: Mark database records as processed
7. **Notification**: Send SQS message with upload status
8. **Cleanup**: Flush logs and clean up resources

The function processes customer charge data by generating tab-separated files and uploading them to S3, with comprehensive error handling and status tracking throughout the process.