using Amazon;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WebAPI.Services
{
    public class StorageService
    {
        private readonly IConfiguration _configuration;

        public StorageService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async void UploadFile(string bucket, string location, string contentType, byte[] content)
        {
            using (var client = new AmazonS3Client())
            {
                using (var stream = new MemoryStream(content))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = location,
                        InputStream = stream,
                        AutoCloseStream = true,
                        ContentType = contentType
                    };

                    await client.PutObjectAsync(putRequest);
                }
            }
        }

        public async void MultipartUploadFile(string bucket, string location, string contentType, byte[] file)
        {
            using (var client = new AmazonS3Client())
            {
                var initiateRequest = new InitiateMultipartUploadRequest()
                {
                    BucketName = "l8-bluewhale",
                    Key = "location",
                };

                var initiateResponse = await client.InitiateMultipartUploadAsync(initiateRequest);
                using (var stream = new MemoryStream(file))
                {

                    var partSize = 100 * 1024 * 1024; //100 MB part size

                    var partNumber = 1;
                    var uploadParts = new List<UploadPartResponse>();

                    var remainingBytes = file.Length;

                    while (remainingBytes > 0)
                    {
                        var partLength = Math.Min(partSize, remainingBytes);
                        remainingBytes -= partLength;
                        var buffer = new byte[partLength];
                        stream.Read(buffer, 0, (int)partLength);

                        var uploadPartRequest = new UploadPartRequest
                        {
                            BucketName = bucket,
                            Key = location,
                            UploadId = initiateResponse.UploadId,
                            PartNumber = partNumber,
                            PartSize = partLength,
                            InputStream = new MemoryStream(buffer),
                        };

                        var uploadPartResponse = await client.UploadPartAsync(uploadPartRequest);
                        uploadParts.Add(uploadPartResponse);

                        partNumber++;
                    }

                    var completeRequest = new CompleteMultipartUploadRequest
                    {
                        BucketName = bucket,
                        Key = location,
                        UploadId = initiateResponse.UploadId,
                        PartETags = uploadParts.Select(p => new PartETag { PartNumber = p.PartNumber, ETag = p.ETag }).ToList(),
                    };

                    var completeResponse = await client.CompleteMultipartUploadAsync(completeRequest);

                }
            }
        }

        public async Task<byte[]> GetFile(string bucket, string location)
        {
            using (var client = new AmazonS3Client())
            {
                try
                {
                    var file = await client.GetObjectAsync(bucket, location);
                
                using (var reader = new BinaryReader(file.ResponseStream))
                {
                    return reader.ReadBytes((int)file.ContentLength);
                }
                }
                catch (Exception ex)
                {
                    return null;
                }

            }
        }

        public async Task<List<Tuple<string, long>>> ListFiles(string bucket, string location)
        {
            using (var client = new AmazonS3Client())
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = location
                };

                ListObjectsV2Response response;
                var list = new List<Tuple<string, long>>();

                do
                {
                    response = await client.ListObjectsV2Async(request);

                    list.AddRange(response.S3Objects.Select(o => new Tuple<string, long>(o.Key, o.Size)));

                    // If the response is truncated, set the request ContinuationToken from the NextContinuationToken property of the response.
                    request.ContinuationToken = response.NextContinuationToken;
                }
                while (response.IsTruncated);

                return list;
            }
        }

        public async void DeleteFile(string bucketName, string location)
        {
            using (var client = new AmazonS3Client())
            {
                await client.DeleteObjectAsync(bucketName, location);
            }
        }

        public async void DeleteFolder(string bucketName, string location)
        {
            using (var client = new AmazonS3Client())
            {
                ListObjectsRequest listObjectsRequest = new ListObjectsRequest
                {
                    BucketName = bucketName,
                    Prefix = location
                };

                DeleteObjectsRequest deleteObjectsRequest = new DeleteObjectsRequest
                {
                    BucketName = bucketName
                };

                ListObjectsResponse listObjectsResponse = null;
                do
                {
                    listObjectsResponse = await client.ListObjectsAsync(listObjectsRequest);
                    foreach (var obj in listObjectsResponse.S3Objects.OrderBy((S3Object x) => x.Key))
                    {
                        deleteObjectsRequest.AddKey(obj.Key);
                        if (deleteObjectsRequest.Objects.Count == 1000)
                        {
                            await client.DeleteObjectsAsync(deleteObjectsRequest);
                            deleteObjectsRequest.Objects.Clear();
                        }

                        listObjectsRequest.Marker = obj.Key;
                    }
                }
                while (listObjectsResponse.IsTruncated);

                if (deleteObjectsRequest.Objects.Count > 0)
                {
                    await client.DeleteObjectsAsync(deleteObjectsRequest);
                }
            }
        }
    }
}
