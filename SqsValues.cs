using Altaworx.AWS.Core.Models;
using Amazon.Lambda.SQSEvents;
using Amop.Core.Constants;

namespace AltaworxSFTPUploadCustomerChargeLambda
{
    public class SqsValues
    {
        public long QueueId { get; set; }
        public int PortalTypeId { get; set; }
        public string InstanceIds { get; set; }

        public SqsValues()
        {
            QueueId = 0;
            PortalTypeId = 0;
            InstanceIds = string.Empty;
        }

        public SqsValues(AmopLambdaContext context, SQSEvent.SQSMessage message)
        {
            if (context == null)
            {
                ArgumentNullException.ThrowIfNull(context);
            }
            if (message == null)
            {
                ArgumentNullException.ThrowIfNull(message);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.PORTAL_TYPE_ID))
            {
                PortalTypeId = Convert.ToInt32(message.MessageAttributes[SQSMessageKeyConstant.PORTAL_TYPE_ID].StringValue);
                context.LogInfo(SQSMessageKeyConstant.PORTAL_TYPE_ID, PortalTypeId.ToString());
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.INSTANCE_IDS))
            {
                InstanceIds = message.MessageAttributes[SQSMessageKeyConstant.INSTANCE_IDS].StringValue;
                context.LogInfo(SQSMessageKeyConstant.INSTANCE_IDS, InstanceIds);
            }

            if (message.MessageAttributes.ContainsKey(SQSMessageKeyConstant.QUEUE_ID))
            {
                QueueId = Convert.ToInt64(message.MessageAttributes[SQSMessageKeyConstant.QUEUE_ID].StringValue);
                context.LogInfo(SQSMessageKeyConstant.QUEUE_ID, QueueId);
            }
        }
    }
}
