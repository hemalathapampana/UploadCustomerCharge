using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Amop.Core.Logger;

namespace Altaworx.AWS.Core.Repositories.OptimizationQueue
{
    public class OptimizationQueueRepository : IOptimizationQueueRepository
    {
        private readonly string _connectionString;
        private readonly IKeysysLogger _logger;

        public OptimizationQueueRepository(IKeysysLogger logger, string connectionString)
        {
            _logger = logger;
            _connectionString = connectionString;
        }

        public Core.OptimizationQueue GetQueue(long queueId)
        {
            _logger.LogInfo("SUB", $"GetQueue({queueId})");
            var queue = new Core.OptimizationQueue();
            using (var conn = new SqlConnection(_connectionString))
            {
                using (var cmd =
                    new SqlCommand("SELECT Id, InstanceId, CommPlanGroupId FROM OptimizationQueue WHERE Id = @queueId",
                        conn))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("@queueId", queueId);
                    conn.Open();

                    var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        queue = QueueFromReader(rdr);
                    }

                    conn.Close();
                }
            }

            return queue;
        }

        private static Core.OptimizationQueue QueueFromReader(IDataRecord rdr)
        {
            return new Core.OptimizationQueue
            {
                Id = long.Parse(rdr["Id"].ToString()),
                InstanceId = long.Parse(rdr["InstanceId"].ToString()),
                CommPlanGroupId = long.Parse(rdr["CommPlanGroupId"].ToString())
            };
        }
    }
}
