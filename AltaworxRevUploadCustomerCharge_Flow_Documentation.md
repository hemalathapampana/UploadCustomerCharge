# AltaworxRevUploadCustomerCharge Lambda Function Flow Documentation

## Overview
This Lambda function processes customer charge data by uploading device customer charge lists to S3 and managing the optimization queue processing workflow. The function is triggered by SQS events and handles customer charge file generation and upload operations.

---

## Flow Breakdown (From First to Last)

### 1. FunctionHandler (Entry Point)
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 35-51

- **Initializes the AmopLambdaContext** using base class method `BaseAmopFunctionHandler`
- **Retrieves environment variables** for S3 bucket and SQS queue configurations
- **Initializes repositories and services** required for database and AWS operations
- **Processes the incoming SQS event** and handles any exceptions with proper logging and cleanup

### 2. BaseAmopFunctionHandler
**File:** `AwsFunctionBase.cs` - Lines 47-51

- **Creates AmopLambdaContext instance** with Lambda context and OU-specific logic flag
- **Returns configured context** for use throughout the Lambda execution
- **Handles base initialization** including logging and environment setup

### 3. TryGetAllEnvironmentVariables
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 69-73

- **Retrieves ProcessUploadedCustomerChargeQueueURL** from environment variables using helper method
- **Retrieves CustomerChargesS3BucketName** from environment variables for S3 operations
- **Validates environment variable availability** and throws exceptions if not configured properly

### 4. InitializeRepositories
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 53-60

- **Creates Base64Service instance** for decoding encrypted configuration values
- **Initializes CustomerChargeRepository** with central database connection string
- **Initializes SettingsRepository** with logger, connection string, and base64 service
- **Initializes OptimizationQueueRepository and OptimizationInstanceRepository** for queue management

### 5. InitializeServices
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 62-67

- **Retrieves GeneralProviderSettings** from settings repository for AWS credentials
- **Creates S3Wrapper instance** with AWS credentials and customer charges S3 bucket name
- **Initializes CustomerChargeListFileService** for generating charge list files

### 6. ProcessEvent
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 75-93

- **Validates SQS event contains records** and logs the number of records to process
- **Iterates through each SQS record** and extracts message attributes
- **Creates SqsValues object** from context and SQS message for structured data access
- **Processes each queue individually** by calling ProcessQueueAsync for each record

### 7. SqsValues Constructor
**File:** `SqsValues.cs` - Lines 20-48

- **Extracts PortalTypeId** from SQS message attributes and logs the value
- **Extracts InstanceIds** from SQS message attributes for optimization instance identification
- **Extracts QueueId** from SQS message attributes for queue processing
- **Validates message structure** and throws exceptions for null parameters

### 8. GetQueue (OptimizationQueueRepository)
**File:** `OptimizationQueueRepository.cs` - Lines 19-44

- **Executes SQL query** to retrieve optimization queue details by queue ID
- **Constructs OptimizationQueue object** with Id, InstanceId, and CommPlanGroupId
- **Handles database connection** opening and closing with proper resource disposal
- **Returns populated queue object** for further processing

### 9. GetInstance (OptimizationInstanceRepository)
**File:** `OptimizationInstanceRepository.cs` - Lines 21-58

- **Executes stored procedure** GET_OPTIMIZATION_INSTANCE_BY_ID to retrieve instance details
- **Maps database results** to OptimizationInstance object with all required properties
- **Handles SQL exceptions** and logs appropriate error messages
- **Returns instance object** containing billing periods, tenant information, and optimization settings

### 10. LoadOptimizationSettingsByTenantId (KeySysLambdaContext)
**File:** `KeySysLambdaContext.cs` - Lines 192-198

- **Validates tenant ID** is greater than zero before proceeding
- **Retrieves optimization settings** specific to the tenant using settings repository
- **Updates context optimization settings** with tenant-specific configurations
- **Ensures proper tenant isolation** for multi-tenant optimization processing

### 11. ProcessQueueAsync
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 95-110

- **Logs queue and instance information** for tracking and debugging purposes
- **Retrieves service provider list** using ServiceProviderCommon.GetServiceProviders
- **Gets device customer charge list** from repository based on queue ID and portal type
- **Validates device list existence** and processes customer charges if devices are found

### 12. GetServiceProviders (ServiceProviderCommon)
**File:** `ServiceProviderCommon.cs` - Lines 83-118

- **Executes SQL query** to retrieve all service providers with their configurations
- **Maps database results** to ServiceProvider objects with integration and billing settings
- **Handles database connection** lifecycle and proper resource disposal
- **Returns complete list** of service providers for charge processing

### 13. GetDeviceCustomerChargeList (CustomerChargeRepository)
**File:** `CustomerChargeRepository.cs` - Lines 70-98

- **Checks optimization settings** for cross-provider customer optimization flag
- **Executes appropriate stored procedure** based on optimization configuration (cross-provider or standard)
- **Uses SQL retry policy** for resilient database operations
- **Returns enumerable collection** of DeviceCustomerChargeQueueRecord objects

### 14. ProcessCustomerCharge
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 112-120

- **Processes device list** by marking records as processed in the database
- **Uploads device list to S3** and waits for completion confirmation
- **Checks upload progress** and sends status message to next processing queue
- **Coordinates file generation** and cloud storage operations

### 15. ProcessDeviceList
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 139-146

- **Iterates through each device** in the customer charge list
- **Marks each record as processed** using CustomerChargeRepository.MarkRecordProcessed
- **Updates database status** with charge ID set to -1 indicating upload processing
- **Ensures data consistency** between processing states

### 16. MarkRecordProcessed (CustomerChargeRepository)
**File:** `CustomerChargeRepository.cs` - Lines 188-206

- **Executes stored procedure** CUSTOMER_CHARGE_UPDATE_DEVICE_CUSTOMER_CHARGE_QUEUE
- **Updates record status** with processing information and charge amounts
- **Uses SQL retry policy** for resilient database operations
- **Handles charge amounts** including base rates and SMS charges

### 17. UploadDeviceListToS3
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 122-137

- **Generates unique filename** using queue ID with .txt extension
- **Creates charge list file bytes** using CustomerChargeListFileService
- **Uploads file to S3** using S3Wrapper with proper error handling
- **Waits for upload completion** with timeout and retry mechanisms

### 18. GenerateChargeListFile (CustomerChargeListFileService)
**File:** `CustomerChargeListFileService.cs` - Lines 12-31

- **Creates memory stream** for efficient file generation in memory
- **Writes file header** with column definitions for charge data
- **Writes file body** with device charge records and billing period information
- **Returns byte array** of generated file content for S3 upload

### 19. WriteChargeListFileHeader (CustomerChargeListFileService)
**File:** `CustomerChargeListFileService.cs` - Lines 53-57

- **Writes tab-separated header row** with all required column names
- **Includes charge information fields** like MSISDN, ChargeId, ChargeAmount
- **Adds billing period fields** for start and end dates
- **Provides error message column** for troubleshooting failed charges

### 20. WriteChargeListFileBody (CustomerChargeListFileService)
**File:** `CustomerChargeListFileService.cs` - Lines 59-75

- **Iterates through charge list** and processes each charge record
- **Determines integration ID** from service providers for billing period calculation
- **Builds billing period strings** using RevIOHelper.BuildBillingPeriodDay
- **Writes individual charge rows** and adds file footer with totals

### 21. WriteChargeRow (CustomerChargeListFileService)
**File:** `CustomerChargeListFileService.cs` - Lines 94-103

- **Determines success status** based on processing flag and charge IDs
- **Formats charge and SMS charge IDs** as strings for successful charges
- **Sanitizes error messages** by removing problematic characters (carriage returns, newlines, tabs)
- **Writes tab-separated data row** with all charge information and billing dates

### 22. UploadAwsFile (S3Wrapper)
**File:** `S3Wrapper.cs` - Lines 138-160

- **Creates memory stream** from byte array for efficient upload
- **Constructs PutObjectRequest** with bucket name, key, and input stream
- **Executes S3 upload operation** using AWS SDK client
- **Returns filename** on successful upload or empty string on failure

### 23. WaitForFileUploadCompletion (S3Wrapper)
**File:** `S3Wrapper.cs` - Lines 269-313

- **Monitors file upload progress** by checking S3 object metadata
- **Implements retry mechanism** with configurable timeout and delay
- **Validates file size consistency** to ensure upload completion
- **Returns success/failure status** with error messages for troubleshooting

### 24. CheckUploadCustomerChargeProgress
**File:** `AltaworxRevUploadCustomerCharge.cs` - Lines 148-160

- **Constructs message attributes** with queue ID, tenant ID, portal type, and success status
- **Sends SQS message** to ProcessUploadedCustomerChargeQueueURL for next processing stage
- **Uses SqsService** for reliable message delivery with retry policies
- **Coordinates workflow progression** to subsequent Lambda functions

### 25. SendSQSMessage (SqsService)
**File:** `SqsService.cs` - Lines 17-55

- **Validates destination queue URL** format and availability
- **Creates SendMessageRequest** with message body and attributes
- **Adds message attributes** as key-value pairs with string data types
- **Executes message send** with Polly retry policy for resilience

### 26. CleanUp (AwsFunctionBase)
**File:** `AwsFunctionBase.cs` - Lines 53-56

- **Flushes logger buffers** to ensure all log messages are written
- **Releases context resources** and performs cleanup operations
- **Ensures proper resource disposal** for memory management
- **Completes Lambda execution** with clean state

---

## Key Data Flow Summary

1. **SQS Event → Context Initialization → Environment Setup**
2. **Repository/Service Initialization → Queue/Instance Retrieval**
3. **Device List Retrieval → Service Provider Loading**
4. **File Generation → S3 Upload → Upload Verification**
5. **Database Status Updates → Progress Notification → Cleanup**

## Error Handling Strategy

Each major operation includes:
- **SQL retry policies** for database resilience
- **Exception logging** with stack traces
- **Resource cleanup** in finally blocks
- **Status validation** before proceeding to next steps

## Dependencies and Integrations

- **AWS S3** for file storage
- **AWS SQS** for message queuing
- **SQL Server** for data persistence
- **Multiple repositories** for data access abstraction
- **Service layer pattern** for business logic separation