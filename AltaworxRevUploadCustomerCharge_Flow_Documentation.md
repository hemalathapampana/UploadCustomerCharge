# AltaworxRevUploadCustomerCharge Lambda - Low Level Flow Documentation

## Overview
The AltaworxRevUploadCustomerCharge lambda is designed to process customer charge data by retrieving device information from a queue, generating charge list files, uploading them to S3, and notifying downstream processes through SQS.

## Main Lambda Flow (AltaworxRevUploadCustomerCharge.cs)

### 1. **FunctionHandler - Entry Point**
The lambda starts by receiving SQS events and initializes the AMOP lambda context through the base class. It retrieves environment variables for S3 bucket names and queue URLs, then initializes all required repositories and services. Any exceptions during processing are logged with full stack traces, and cleanup is performed at the end regardless of success or failure.

### 2. **InitializeRepositories - Database Layer Setup**
Sets up all database access repositories including CustomerChargeRepository for device charge data, SettingsRepository for configuration settings, and OptimizationQueueRepository/OptimizationInstanceRepository for queue and instance management. Each repository is initialized with the central database connection string and appropriate logging capabilities for error tracking and debugging.

### 3. **InitializeServices - External Service Setup** 
Retrieves AWS credentials from the settings repository and initializes the S3Wrapper for file operations with the customer charges bucket. Also creates the CustomerChargeListFileService for generating formatted charge list files. These services handle all external interactions including S3 uploads and file generation with proper error handling.

### 4. **ProcessEvent - SQS Message Processing**
Iterates through each SQS message record and extracts queue information using SqsValues parser. For each record, it retrieves the optimization queue and instance details from the database, loads tenant-specific optimization settings, then processes the queue asynchronously. Logs the total number of messages being processed and individual message IDs for tracking purposes.

### 5. **ProcessQueueAsync - Core Business Logic**
Retrieves service provider list and device customer charge data based on the queue ID and portal type. Checks if cross-provider optimization is enabled and adjusts data retrieval accordingly. If no devices are found in the queue, it logs completion; otherwise, it proceeds to process the customer charges by calling the charge processing workflow.

### 6. **ProcessCustomerCharge - Charge Processing Workflow**
Marks all device records as processed in the database with a charge ID of -1 to indicate processing status. Generates a charge list file using the CustomerChargeListFileService and uploads it to S3 with the queue ID as filename. Waits for S3 upload completion with a 5-minute timeout, then sends the processing status to the next queue for downstream processing.

### 7. **UploadDeviceListToS3 - File Upload Management**
Creates a formatted charge list file containing device data, billing periods, and service provider information. Uploads the file to S3 using the queue ID as the filename and waits for upload completion with proper timeout handling. Returns success/failure status with error messages for downstream processing and logging purposes.

### 8. **CheckUploadCustomerChargeProgress - Downstream Notification**
Constructs SQS message attributes containing queue ID, tenant ID, portal type, instance IDs, and success status. Sends this information to the ProcessUploadedCustomerChargeQueueURL for the next stage of processing. This enables the workflow to continue with proper context and status information for subsequent lambda functions.

---

## Supporting Classes

### **AwsFunctionBase.cs - Base Infrastructure**
Provides foundational AWS lambda functionality including logging infrastructure, database connection management, and AWS credential handling. Contains utility methods for retrieving environment variables, SQL bulk copy operations, and customer/instance data retrieval. Implements retry policies and error handling patterns used across all derived lambda functions for consistent behavior and reliability.

### **CustomerChargeListFileService.cs - File Generation Engine**
Generates tab-delimited text files containing customer charge information with headers for MSISDN, success status, charge IDs, amounts, and billing periods. Processes device charge records and service provider data to create properly formatted billing files. Handles billing period calculations based on integration types and includes summary totals at the file footer for reconciliation purposes.

### **CustomerChargeRepository.cs - Data Access Layer**
Manages all database operations related to customer charge queues and device records using stored procedures with retry policies. Retrieves device charge lists with support for cross-provider optimization scenarios and different portal types. Updates device processing status and provides queue management operations with comprehensive error handling and logging throughout all database interactions.

### **KeySysLambdaContext.cs - Context Management**
Initializes and manages the lambda execution context including database connections, environment variables, and configuration settings. Handles Redis cache connections for performance optimization and loads organization unit specific settings. Provides logging infrastructure and tenant-specific optimization settings loading with proper cleanup and resource management throughout the lambda lifecycle.

### **OptimizationInstanceRepository.cs - Instance Data Access**
Retrieves optimization instance details including billing periods, tenant information, and service provider associations from the database. Executes stored procedures to fetch instance-specific data required for charge processing workflows. Handles all database exceptions gracefully and provides properly mapped instance objects with comprehensive property mapping from database fields.

### **OptimizationQueueRepository.cs - Queue Management**
Provides simple queue retrieval operations to get queue details including instance ID and communication plan group ID. Executes direct SQL queries against the OptimizationQueue table with proper parameter binding and connection management. Maps database records to queue objects and handles connection lifecycle with appropriate error logging for debugging purposes.

### **S3Wrapper.cs - Cloud Storage Interface**
Manages all S3 operations including bucket creation, file uploads, and upload completion monitoring with configurable timeouts. Provides methods for file existence checking, object listing, and deletion operations with proper AWS credential management. Implements upload progress monitoring with retry logic and status reporting for reliable file transfer operations in cloud environments.

### **ServiceProviderCommon.cs - Provider Data Access**
Retrieves service provider information including integration details, billing configurations, and tenant associations from the database. Provides methods for getting individual providers by ID or name, and retrieving complete provider lists with all configuration details. Handles database connections and query execution with proper field mapping and null value handling for comprehensive provider data access.

### **SettingsRepository.cs - Configuration Management**
Manages retrieval of various configuration settings including AWS credentials, optimization parameters, and provider-specific settings like Jasper, Telegence, and eBonding configurations. Implements retry policies for database operations and handles Base64 decoding of sensitive credentials. Provides tenant-specific optimization settings and timezone handling with comprehensive mapping of setting keys to strongly-typed configuration objects.

### **SqlQueryHelper.cs - Database Utilities**
Provides generic database operation utilities for executing stored procedures with different return types including lists, scalars, and row counts. Implements comprehensive error handling with configurable exception throwing and detailed logging of SQL operations. Includes parameter cloning utilities and connection management with timeout configurations for reliable database interactions across all repository classes.

### **SqsService.cs - Message Queue Interface**
Handles SQS message sending operations with AWS credential management and regional endpoint configuration. Supports message attributes, delay seconds, and retry policies for reliable message delivery. Implements URL validation and comprehensive error handling with detailed logging of message operations for tracking and debugging distributed workflow communications.

### **SqsValues.cs - Message Parsing**
Parses SQS message attributes to extract queue ID, portal type ID, and instance IDs from incoming messages. Provides structured access to message data with null checking and type conversion handling. Logs extracted values for debugging and provides clean object-oriented access to message parameters used throughout the lambda processing workflow.

---

## Key Integration Points

1. **Database Integration**: Multiple repositories handle different aspects of data access with comprehensive error handling and retry policies
2. **AWS S3 Integration**: File upload and management with progress monitoring and timeout handling  
3. **SQS Integration**: Message parsing for input and message sending for output with proper attribute handling
4. **Configuration Management**: Centralized settings retrieval with encryption/decryption support and tenant-specific overrides
5. **Error Handling**: Comprehensive logging and exception management throughout all layers with stack trace preservation
6. **Resource Management**: Proper cleanup of database connections, AWS clients, and other resources with disposal patterns

## Processing Flow Summary

1. **Initialization**: Context setup, environment variables, repositories, and services
2. **Message Processing**: SQS event parsing and queue/instance data retrieval
3. **Data Retrieval**: Device charge list extraction with provider information
4. **File Generation**: Formatted charge list file creation with billing period calculations
5. **Cloud Upload**: S3 file upload with completion monitoring and status tracking
6. **Status Update**: Database record marking and downstream SQS notification
7. **Cleanup**: Resource disposal and logging finalization