using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Amop.Core.Logger;
using Amop.Core.Constants;
using Amop.Core.Helpers;

namespace Altaworx.AWS.Core.Repositories.OptimizationInstance
{
    public class OptimizationInstanceRepository : IOptimizationInstanceRepository
    {
        private readonly IKeysysLogger _logger;
        private readonly string _connectionString;

        public OptimizationInstanceRepository(IKeysysLogger logger, string connectionString)
        {
            _logger = logger;
            _connectionString = connectionString;
        }

        public Core.OptimizationInstance GetInstance(long instanceId)
        {
            _logger.LogInfo(CommonConstants.SUB, $"GetInstance({instanceId})");
            var optimizationInstance = new Core.OptimizationInstance();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    using (var command = new SqlCommand(SQLConstant.StoredProcedureName.GET_OPTIMIZATION_INSTANCE_BY_ID, connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@instanceId", instanceId);
                        command.CommandTimeout = SQLConstant.ShortTimeoutSeconds;
                        connection.Open();

                        var optimizationInstanceDataReader = command.ExecuteReader();
                        while (optimizationInstanceDataReader.Read())
                        {
                            optimizationInstance = InstanceFromReader(optimizationInstanceDataReader);
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogInfo(CommonConstants.EXCEPTION, string.Format(LogCommonStrings.EXCEPTION_WHEN_EXECUTING_SQL_COMMAND, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogInfo(CommonConstants.EXCEPTION, string.Format(LogCommonStrings.EXCEPTION_WHEN_CONNECTING_DATABASE, ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogInfo(CommonConstants.EXCEPTION, ex.Message);
            }

            return optimizationInstance;
        }

        private static Core.OptimizationInstance InstanceFromReader(SqlDataReader optimizationInstanceDataReader)
        {
            var columns = optimizationInstanceDataReader.GetColumnsFromReader();
            return new Core.OptimizationInstance
            {
                Id = optimizationInstanceDataReader.LongFromReader(columns, CommonColumnNames.Id),
                RunStatusId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.RunStatusId),
                RunStartTime = optimizationInstanceDataReader.DateTimeFromReader(columns, CommonColumnNames.RunStartTime),
                RunEndTime = optimizationInstanceDataReader.DateTimeFromReader(columns, CommonColumnNames.RunEndTime),
                BillingPeriodStartDate = optimizationInstanceDataReader.DateTimeFromReader(columns, CommonColumnNames.BillingPeriodStartDate),
                BillingPeriodEndDate = optimizationInstanceDataReader.DateTimeFromReader(columns, CommonColumnNames.BillingPeriodEndDate),
                RevCustomerId = optimizationInstanceDataReader.GuidFromReader(columns, CommonColumnNames.RevCustomerId),
                ServiceProviderId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.ServiceProviderId),
                IntegrationAuthenticationId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.IntegrationAuthenticationId),
                IsBillInAdvanceEligible = optimizationInstanceDataReader.BooleanFromReader(columns, CommonColumnNames.UseBillInAdvance),
                AMOPCustomerId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.AMOPCustomerId),
                TenantId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.TenantId),
                OptimizationSessionId = optimizationInstanceDataReader.IntFromReader(columns, CommonColumnNames.OptimizationSessionId)
            };
        }
    }
}
