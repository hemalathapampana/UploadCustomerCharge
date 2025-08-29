# AltaworxRevUploadCustomerCharge Lambda - Low Level Flow Documentation

## Overview
This Lambda function processes SQS events to upload customer charge data to S3 buckets. The function processes device customer charge records in batches, generates charge list files, uploads them to S3, and sends progress notifications through SQS. It operates as part of an optimization workflow for telecommunications billing and customer charge management.

## Main Lambda Function (AltaworxRevUploadCustomerCharge.cs)

### FunctionHandler Entry Point
The `FunctionHandler` method serves as the main entry point for processing SQS events from AWS Lambda. It initializes the Lambda context using the base class, retrieves environment variables for S3 bucket and queue configurations, and sets up all required repositories and services. The method processes each SQS record in the event and handles any exceptions by logging them before cleaning up resources. This is a typical AWS Lambda pattern that ensures proper resource management and error handling.

### Environment Variable Setup (TryGetAllEnvironmentVariables)
The method retrieves critical configuration values from environment variables including the S3 bucket name for customer charges and the SQS queue URL for processing notifications. It uses the `EnvironmentRepository` to safely fetch these values and will throw exceptions if required variables are not configured. This configuration-driven approach allows the Lambda to be deployed across different environments without code changes. The environment variables control where charge files are stored and where progress notifications are sent.

### Repository and Service Initialization
The `InitializeRepositories` method creates instances of all data access repositories including customer charge, settings, optimization queue, and optimization instance repositories. Each repository is initialized with the central database connection string and appropriate logging services for tracking operations. The `InitializeServices` method then creates the S3 wrapper and customer charge list file service using the general provider settings. This separation of concerns allows for proper dependency injection and makes the code more testable and maintainable.

### SQS Event Processing (ProcessEvent)
The method iterates through each SQS record in the incoming event and extracts queue processing information using the `SqsValues` class. For each record, it retrieves the associated optimization queue and instance data from the database to understand the processing context. The method loads tenant-specific optimization settings and calls the main queue processing logic for each record. This approach allows the Lambda to handle multiple queue processing requests in a single invocation while maintaining proper isolation between different optimization instances.

### Queue Processing Logic (ProcessQueueAsync)
This method loads service provider information and retrieves the list of device customer charge records for the specified queue and portal type. If no devices are found, it logs that all items in the queue have been processed and exits gracefully. When devices are present, it calls the customer charge processing method to handle file generation and upload. The method also logs optimization settings to provide visibility into the configuration being used for processing.

### Customer Charge Processing (ProcessCustomerCharge)
The method marks all device records as processed in the database before attempting to upload the charge file to S3. It generates a charge list file using the device data and service provider information, then uploads it to the configured S3 bucket. After the upload attempt, it checks the progress and sends an SQS notification with the results including success/failure status and relevant metadata. This ensures that downstream processes are notified of the completion status and can take appropriate action.

### S3 File Upload (UploadDeviceListToS3)
The method creates a filename based on the queue ID and generates the charge list file content using the `CustomerChargeListFileService`. It uploads the file to S3 using the configured bucket and waits for upload completion with a timeout mechanism. The method returns a boolean indicating success or failure, with detailed error messages logged for troubleshooting. This approach ensures that files are fully uploaded before proceeding with subsequent processing steps.

### Device Processing (ProcessDeviceList)
This method iterates through each device record and marks it as processed in the database using the `CustomerChargeRepository`. The marking includes updating the processed status and any associated charge information with a charge ID of -1 indicating upload processing. This step ensures that devices are not reprocessed in subsequent runs and maintains data consistency. The method operates on each device individually to handle potential failures gracefully.

### Progress Notification (CheckUploadCustomerChargeProgress)
The method constructs an SQS message with attributes including queue ID, tenant ID, portal type ID, instance IDs, and success status. It sends this message to the configured process queue URL to notify downstream components of the upload completion. This notification mechanism allows other parts of the system to react to upload events and continue processing workflows. The message attributes provide all necessary context for downstream processing decisions.

## Supporting Classes

### AwsFunctionBase.cs - Base Lambda Functionality
This abstract base class provides common functionality for all AWS Lambda functions in the system including logging, database access, and AWS credential management. It contains methods for retrieving optimization instances and queues from the database using stored procedures with proper error handling and connection management. The class provides AWS credential creation methods for both general AWS access and SES-specific operations using Base64-decoded secrets. It includes utility methods for environment variable retrieval, SQL bulk copy operations, and parameterized logging that ensure consistent behavior across all Lambda functions.

### CustomerChargeListFileService.cs - File Generation
This service class is responsible for generating tab-delimited text files containing customer charge information for upload processing. It creates files with headers including MSISDN, success status, charge IDs, amounts, billing periods, and error messages. The service handles billing period calculations using service provider integration settings and formats charge data appropriately. The generated files serve as the data source for downstream billing and reporting systems.

### CustomerChargeRepository.cs - Data Access Layer
This repository provides database access methods for customer charge operations including creating queues, retrieving device records, and updating processing status. It uses SQL retry policies to handle transient database failures and supports both regular and cross-provider optimization scenarios. The repository executes stored procedures for complex operations and provides mapping functions to convert database records to domain objects. All database operations include proper parameter handling and logging for auditability and troubleshooting.

### KeySysLambdaContext.cs - Context Management
This class encapsulates the Lambda execution context and provides access to configuration settings, database connections, and logging services. It initializes environment-specific settings from environment variables and loads optimization settings based on tenant context. The class manages Redis cache connections for performance optimization and provides methods for loading tenant-specific configurations. It handles the complete lifecycle of Lambda context including initialization, operation, and cleanup phases.

### OptimizationInstanceRepository.cs - Instance Data Access
This repository manages access to optimization instance data which contains billing periods, customer information, and processing status. It retrieves instance details using stored procedures and maps database records to strongly-typed objects with proper null handling. The repository includes comprehensive error handling for database connectivity and SQL execution issues. Instance data drives the processing logic and determines how customer charges are calculated and processed.

### OptimizationQueueRepository.cs - Queue Management
This lightweight repository provides access to optimization queue data which links processing requests to specific optimization instances. It retrieves queue information including instance ID and communication plan group ID using simple SQL queries. The repository follows the same error handling patterns as other data access components and provides mapping from database records to domain objects. Queue data serves as the entry point for processing workflows and determines which optimization instance should handle the request.

### S3Wrapper.cs - AWS S3 Integration
This wrapper class provides comprehensive AWS S3 functionality including bucket creation, file upload, download, and management operations. It handles AWS credential configuration and regional endpoint setup for consistent S3 access across the application. The class includes file upload completion detection with timeout mechanisms to ensure files are fully processed before continuing. It provides both synchronous and asynchronous operations with proper error handling and status reporting for integration with the broader application workflow.

### ServiceProviderCommon.cs - Provider Data Access
This static utility class provides methods for retrieving service provider information from the database including provider details, integration settings, and billing configurations. It supports queries by provider ID, name, or integration type to accommodate different lookup scenarios throughout the application. The class includes comprehensive provider data mapping with proper null handling for optional fields like tenant ID and billing settings. Service provider data is essential for determining how customer charges are calculated and processed for different telecommunications carriers.

### SettingsRepository.cs - Configuration Management
This repository manages application settings including general provider settings, optimization settings, and AWS credentials with proper encryption handling. It retrieves settings from the database and maps them to strongly-typed configuration objects with Base64 decoding for sensitive data like AWS secret keys. The repository supports tenant-specific settings overrides and provides timezone configuration for billing period calculations. Settings drive the behavior of the entire optimization and billing workflow.

### SqlQueryHelper.cs - Database Utilities
This helper class provides reusable methods for executing stored procedures and SQL commands with consistent error handling and logging. It supports both list results and scalar results with proper parameter handling and connection management. The helper includes retry logic for transient failures and provides detailed logging for troubleshooting database operations. All database access in the application flows through these helper methods to ensure consistent behavior and error handling.

### SqsService.cs - Message Queue Integration
This service class handles sending messages to AWS SQS queues with proper credential management and error handling. It supports message attributes for passing structured data and includes retry policies for handling transient AWS service failures. The service validates queue URLs and formats messages appropriately for downstream processing. SQS integration enables decoupled communication between different components of the optimization workflow.

### SqsValues.cs - Message Data Structure
This class encapsulates the data structure for SQS message attributes including queue ID, portal type ID, and instance IDs. It provides parsing logic for extracting these values from incoming SQS messages with proper validation and error handling. The class serves as a data transfer object between the SQS event processing and the business logic components. It ensures that all necessary processing context is available throughout the workflow execution.

## Data Flow Summary

1. **Initialization**: Lambda receives SQS event → Creates context → Loads environment variables → Initializes repositories and services
2. **Message Processing**: Extracts SQS values → Retrieves queue and instance data → Loads optimization settings → Processes each device record
3. **File Generation**: Retrieves service provider data → Generates charge list file → Uploads to S3 → Waits for completion confirmation
4. **Status Updates**: Marks device records as processed → Constructs progress notification → Sends SQS message with results
5. **Cleanup**: Handles any errors → Logs completion status → Releases resources and context

The entire workflow is designed for reliability with retry policies, comprehensive logging, and proper error handling at each step to ensure customer charge data is processed accurately and completely.