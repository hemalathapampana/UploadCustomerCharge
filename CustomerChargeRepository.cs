using System;
using System.Collections.Generic;
using Altaworx.AWS.Core.Models;
using Amop.Core.Constants;
using Amop.Core.Enumerations;
using Amop.Core.Helpers;
using Amop.Core.Helpers.Pond;
using Amop.Core.Logger;
using Amop.Core.Models.Revio;
using Amop.Core.Resilience;
using Microsoft.Data.SqlClient;
using Polly;
using static Altaworx.AWS.Core.RevIOCommon;

namespace Altaworx.AWS.Core.Repositories.CustomerCharge
{
    public class CustomerChargeRepository
    {
        private const int MaxRetries = PondHelper.CommonConfig.RETRY_NUMBER;
        private readonly string connectionString;
        private readonly ISyncPolicy sqlRetryPolicy;

        public CustomerChargeRepository(string connectionString)
            : this(connectionString, new NoOpLogger())
        {
        }

        public CustomerChargeRepository(string connectionString, IKeysysLogger logger)
            : this(connectionString, new PolicyFactory(logger))
        {
        }

        public CustomerChargeRepository(string connectionString, IPolicyFactory policyFactory)
            : this(connectionString, policyFactory.GetSqlRetryPolicy(MaxRetries))
        {
        }

        public CustomerChargeRepository(string connectionString, ISyncPolicy sqlRetryPolicy)
        {
            this.connectionString = connectionString;
            this.sqlRetryPolicy = sqlRetryPolicy;
        }

        public List<long> CreateAndGetCustomerChargeQueue(Action<string, string> logFunction, string instanceIds, int portalTypeId)
        {
            var storedProcedureName = SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_CREATE_AND_GET_CUSTOMER_CHARGE_QUEUE;
            if (portalTypeId == (int)PortalTypeEnum.Mobility)
            {
                storedProcedureName = SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_CREATE_AND_GET_MOBILITY_CUSTOMER_CHARGE_QUEUE;
            }
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.INSTANCE_IDS, instanceIds),
            };
            var queuesToProcess = sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                    storedProcedureName,
                    (dataReader) => ReadCustomerChargeQueuesToProcess(dataReader),
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
            return queuesToProcess;
        }

        private long ReadCustomerChargeQueuesToProcess(SqlDataReader dataReader)
        {
            var columns = dataReader.GetColumnsFromReader();
            return dataReader.LongFromReader(columns, CommonColumnNames.QueueId);
        }

        public IEnumerable<DeviceCustomerChargeQueueRecord> GetDeviceCustomerChargeList(AmopLambdaContext context, Action<string, string> logFunction, long queueId, int portalTypeId)
        {
            if (context.OptimizationSettings.OptIntoCrossProviderCustomerOptimization)
            {
                var crossParameters = new List<SqlParameter>()
                {
                new SqlParameter(CommonSQLParameterNames.QUEUE_ID, queueId),
                };
                var crossQueuesToProcess = sqlRetryPolicy.Execute(() =>
                    SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                        SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_GET_DEVICE_CROSS_CUSTOMER_CHARGE,
                        (dataReader) => ReadDeviceRecordFromReader(dataReader),
                        crossParameters,
                        SQLConstant.ShortTimeoutSeconds));
                return crossQueuesToProcess;
            }
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.QUEUE_ID, queueId),
                new SqlParameter(CommonSQLParameterNames.PORTAL_TYPE_ID, portalTypeId),
            };
            var queuesToProcess = sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                    SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_GET_DEVICE_CUSTOMER_CHARGE,
                    (dataReader) => ReadDeviceRecordFromReader(dataReader),
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
            return queuesToProcess;
        }

        public IEnumerable<DeviceCustomerChargeQueueRecord> GetAllDeviceCustomerChargeByInstanceIds(AmopLambdaContext context, Action<string, string> logFunction, string instanceIds, int portalTypeId)
        {
            if (context.OptimizationSettings.OptIntoCrossProviderCustomerOptimization)
            {
                var crossParameters = new List<SqlParameter>()
                {
                new SqlParameter(CommonSQLParameterNames.INSTANCE_IDS, instanceIds),
                };
                var crossQueuesToProcess = sqlRetryPolicy.Execute(() =>
                    SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                        SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_GET_ALL_DEVICES_CROSS_OPTIMIZATION_BY_INSTANCE_IDS,
                        (dataReader) => ReadDeviceRecordToUploadSftpFile(dataReader),
                        crossParameters,
                        SQLConstant.ShortTimeoutSeconds));
                return crossQueuesToProcess;
            }
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.INSTANCE_IDS, instanceIds),
                new SqlParameter(CommonSQLParameterNames.PORTAL_TYPE_ID, portalTypeId),
            };
            var queuesToProcess = sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                    SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_GET_ALL_DEVICES_BY_INSTANCE_IDS,
                    (dataReader) => ReadDeviceRecordToUploadSftpFile(dataReader),
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
            return queuesToProcess;
        }

        private DeviceCustomerChargeQueueRecord ReadDeviceRecordToUploadSftpFile(SqlDataReader dataReader)
        {
            var columns = dataReader.GetColumnsFromReader();
            return new DeviceCustomerChargeQueueRecord()
            {
                UsageMB = dataReader.DecimalFromReader(columns, CommonColumnNames.UsageMB),
                ChargeAmount = dataReader.DecimalFromReader(columns, CommonColumnNames.ChargeAmount),
                BillingEndDate = dataReader.DateTimeFromReader(columns, CommonColumnNames.BillingEndDate),
                MSISDN = dataReader.StringFromReader(columns, CommonColumnNames.MSISDN),
                ICCID = dataReader.StringFromReader(columns, CommonColumnNames.ICCID),
                RevAccountNumber = dataReader.StringFromReader(columns, CommonColumnNames.RevAccountNumber),
                ServiceProviderId = dataReader.IntFromReader(columns, CommonColumnNames.ServiceProviderId),
                IsBillInAdvance = dataReader.BooleanFromReader(columns, CommonColumnNames.IsBillInAdvance),
            };
        }

        public int UpdateAndCheckQueueProcessing(Action<string, string> logFunction, long queueId, bool isSuccessful)
        {
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.QUEUE_ID, queueId),
                new SqlParameter(CommonSQLParameterNames.IS_SUCCESSFUL, isSuccessful)
            };
            var queuesToProcess = sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithIntResult(logFunction, connectionString,
                    SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_UPDATE_AND_CHECK_QUEUE_PROCESSING,
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
            return queuesToProcess;
        }

        public List<CustomerChargeQueueOfInstance> GetAllCustomerChargeQueueByInstanceIds(Action<string, string> logFunction, string instanceIds, int portalTypeId)
        {
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.INSTANCE_IDS, instanceIds),
                new SqlParameter(CommonSQLParameterNames.PORTAL_TYPE_ID, portalTypeId)
            };

            var result = sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithListResult(logFunction, connectionString,
                    SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_GET_ALL_QUEUE_ID_BY_INSTANCE_IDS,
                    (dataReader) => ReadCustomerChargeQueueFromReader(dataReader),
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
            return result;
        }

        private DeviceCustomerChargeQueueRecord ReadDeviceRecordFromReader(SqlDataReader dataReader)
        {
            return new DeviceCustomerChargeQueueRecord(dataReader);
        }

        private CustomerChargeQueueOfInstance ReadCustomerChargeQueueFromReader(SqlDataReader dataReader)
        {
            return new CustomerChargeQueueOfInstance(dataReader);
        }

        public void MarkRecordProcessed(Action<string, string> logFunction, int portalTypeId, DeviceCustomerChargeQueueRecord device, int chargeId)
        {
            var parameters = new List<SqlParameter>()
            {
                new SqlParameter(CommonSQLParameterNames.PORTAL_TYPE_ID, portalTypeId),
                new SqlParameter(CommonSQLParameterNames.ID, device.Id),
                new SqlParameter(CommonSQLParameterNames.CHARGE_ID, chargeId),
                new SqlParameter(CommonSQLParameterNames.CHARGE_AMOUNT, device.DeviceCharge),
                new SqlParameter(CommonSQLParameterNames.BASE_CHARGE_AMOUNT, device.BaseRate),
                new SqlParameter(CommonSQLParameterNames.TOTAL_CHARGE_AMOUNT, device.DeviceCharge + device.BaseRate),
                new SqlParameter(CommonSQLParameterNames.SMS_CHARGE_ID, device.SmsChargeId),
                new SqlParameter(CommonSQLParameterNames.SMS_CHARGE_AMOUNT, device.SmsChargeAmount),
            };
            sqlRetryPolicy.Execute(() =>
                SqlQueryHelper.ExecuteStoredProcedureWithIntResult(logFunction, connectionString,
                    SQLConstant.StoredProcedureName.CUSTOMER_CHARGE_UPDATE_DEVICE_CUSTOMER_CHARGE_QUEUE,
                    parameters,
                    SQLConstant.ShortTimeoutSeconds));
        }
    }
}
