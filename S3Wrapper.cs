using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3.Model;
using Amop.Core.Constants;
using Amop.Core.Logger;

namespace Altaworx.AWS.Core
{
    public class S3Wrapper : IS3Wrapper
    {
        // These are required to connect to the KeySys AWS account
        private string _bucketName;
        private Amazon.S3.IAmazonS3 _s3Client;
        private bool _isPublicAccessBlockRequest = false;

        public GetObjectResponse ObjectResponse;

        public List<S3Object> S3Objects;

        public bool Success;
        public bool Error;
        public string ErrorMessage;

        public S3Wrapper(BasicAWSCredentials awsCredentials, string setBucket)
        {
            var credentials = awsCredentials;

            _s3Client = new Amazon.S3.AmazonS3Client(credentials, RegionEndpoint.USEast1);

            ListBucketsResponse bucketList = _s3Client.ListBucketsAsync().Result;

            bool found = false;
            foreach (var b in bucketList.Buckets)
                if (b.BucketName == setBucket)
                    found = true;

            if (!found)
            {
                var request = new PutBucketRequest();
                request.BucketName = setBucket;
                request.UseClientRegion = true;

                var response = _s3Client.PutBucketAsync(request).Result;
            }

            _bucketName = setBucket;
            S3Objects = new List<S3Object>();
        }

        public S3Wrapper(BasicAWSCredentials awsCredentials, string setBucket, bool isPublicAccessBlockRequest)
        {
            var credentials = awsCredentials;
            _s3Client = new Amazon.S3.AmazonS3Client(credentials, RegionEndpoint.USEast1);
            _isPublicAccessBlockRequest = isPublicAccessBlockRequest;

            ListBucketsResponse bucketList = _s3Client.ListBucketsAsync().Result;

            bool found = false;
            foreach (var b in bucketList.Buckets)
                if (b.BucketName == setBucket)
                    found = true;

            if (!found)
            {
                var request = new PutBucketRequest();
                request.BucketName = setBucket;
                request.UseClientRegion = true;

                var response = _s3Client.PutBucketAsync(request).Result;
                if (_isPublicAccessBlockRequest)
                {
                    var publicBlockRequest = new PutPublicAccessBlockRequest();
                    publicBlockRequest.BucketName = setBucket;

                    var config = new PublicAccessBlockConfiguration()
                    {
                        BlockPublicAcls = true,
                        BlockPublicPolicy = true,
                        IgnorePublicAcls = true,
                        RestrictPublicBuckets = true,
                    };
                    publicBlockRequest.PublicAccessBlockConfiguration = config;
                    var responsePublicBlock = _s3Client.PutPublicAccessBlockAsync(publicBlockRequest).Result;
                }
            }

            _bucketName = setBucket;
            S3Objects = new List<S3Object>();
        }

        private string GetAwsFileName()
        {
            string path = Path.GetRandomFileName() + Path.GetRandomFileName();
            path = path.Replace(".", ""); // Remove periods.
            return path;
        }

        private void ResetStatus()
        {
            Success = true;
            Error = false;
            ErrorMessage = "";
        }

        //public static string GetMimeType(string FileName)
        //{
        //    return MimeMapping.MimeUtility.GetMimeMapping(FileName);
        //}

        private Stream GetAwsFileStream(string awsFilename)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = awsFilename
            };

            GetObjectResponse response = _s3Client.GetObjectAsync(request).Result;
            BufferedStream stream = new BufferedStream(response.ResponseStream);

            return stream;
        }

        ///<summary>
        ///Retrieves an object from AWS S3.  Sets the mime type of the FileStream
        ///</summary>
        public Stream GetAwsFile(string awsFileName, string fileName)
        {
            Stream stream = GetAwsFileStream(awsFileName);
            return stream;
        }

        public string UploadAwsFile(byte[] fileBytes, string awsFileName)
        {
            using (MemoryStream ms = new MemoryStream(fileBytes))
            {
                return UploadAwsFile(ms, awsFileName);
            }
        }

        public string UploadAwsFile(Stream s, string awsFileName)
        {
            if (s != null && s.Length > 0)
            {
                PutObjectRequest request = new PutObjectRequest();
                request.BucketName = _bucketName;
                request.Key = awsFileName;
                request.InputStream = s;

                var response = _s3Client.PutObjectAsync(request).Result;
                return awsFileName;
            }

            return "";
        }

        public List<S3Object> GetAllObjects()
        {
            List<S3Object> ret = new List<S3Object>();

            ListObjectsRequest request = new ListObjectsRequest();
            request.BucketName = this._bucketName;

            do
            {
                ListObjectsResponse response = _s3Client.ListObjectsAsync(request).Result;

                foreach (S3Object o in response.S3Objects)
                    ret.Add(o);

                if (response.IsTruncated)
                    request.Marker = response.NextMarker;
                else
                    request = null;
            } while (request != null);


            return ret;
        }

        public List<S3Object> GetAllObjects(string prefix)
        {

            S3Objects = new List<S3Object>();

            ListObjectsRequest request = new ListObjectsRequest();
            ListObjectsResponse response = new ListObjectsResponse();
            request.BucketName = this._bucketName;
            request.Prefix = prefix;

            do
            {
                try
                {
                    response = _s3Client.ListObjectsAsync(request).Result;
                }
                catch (Exception ex)
                {
                    Success = false;
                    Error = true;
                    ErrorMessage = "Error - GetAllObjects";
                    return S3Objects;
                }

                S3Objects.AddRange(response.S3Objects);

                if (response.IsTruncated)
                    request.Marker = response.NextMarker;
                else
                    request = null;
            } while (request != null);


            return S3Objects;
        }

        public void DeleteObject(string key)
        {
            DeleteObjectRequest request = new DeleteObjectRequest();
            request.BucketName = this._bucketName;
            request.Key = key;

            try
            {
                DeleteObjectResponse response = _s3Client.DeleteObjectAsync(request).Result;
            }
            catch (Exception ex)
            {
                return;
            }
        }

        public bool ObjectExists(string key)
        {
            ResetStatus();

            if (key == "")
                return false;

            ListObjectsRequest request = new ListObjectsRequest();
            ListObjectsResponse response = new ListObjectsResponse();
            request.BucketName = this._bucketName;
            request.Prefix = key;

            try
            {
                response = _s3Client.ListObjectsAsync(request).Result;
            }
            catch (Exception ex)
            {
                Success = false;
                Error = true;
                ErrorMessage = "Error - ObjectExists - Key = " + key;
                return false;
            }

            foreach (S3Object o in response.S3Objects)
                if (o.Key == key)
                    return true;

            return false;
        }

        public async Task<Tuple<bool, string>> WaitForFileUploadCompletion(string key, int timeoutInSeconds, IKeysysLogger logger = null)
        {
            DateTime startTime = DateTime.UtcNow;
            bool isUploadCompleted = false;
            long contentLengthTemp = 0;
            int retryCount = 0;

            while (!isUploadCompleted && (DateTime.UtcNow - startTime).TotalSeconds < timeoutInSeconds)
            {
                try
                {
                    var response = await _s3Client.GetObjectMetadataAsync(_bucketName, key);
                    if (response.HttpStatusCode == System.Net.HttpStatusCode.OK && response.ContentLength > 0)
                    {
                        if (contentLengthTemp != response.ContentLength)
                        {
                            contentLengthTemp = response.ContentLength;
                        }
                        else
                        {
                            isUploadCompleted = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = string.Format(LogCommonStrings.CAN_NOT_GET_THE_FILE_S3, key, ex.Message);
                }
                retryCount++;
                if (retryCount > 1)
                {
                    //skip logging message for the first retry time
                    logger?.LogInfo(CommonConstants.INFO, string.Format(LogCommonStrings.WATING_FOR_THE_FILE_UPLOAD, key, retryCount, CommonConstants.DELAY_IN_SECONDS_FIVE_SECONDS));
                }
                Thread.Sleep(TimeSpan.FromSeconds(CommonConstants.DELAY_IN_SECONDS_FIVE_SECONDS));
            }

            if (isUploadCompleted)
            {
                logger?.LogInfo(CommonConstants.INFO, string.Format(LogCommonStrings.UPLOAD_FILE_S3_COMPLETED, key, retryCount));
                return new Tuple<bool, string>(true, string.Empty);
            }
            return new Tuple<bool, string>(false, ErrorMessage);
        }
    }
}
