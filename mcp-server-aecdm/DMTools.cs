using ModelContextProtocol.Server;
using System.ComponentModel;
using Newtonsoft.Json.Linq;


using Autodesk.SDKManager;
using System.IO;
using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using Autodesk.DataManagement.Model;
using Autodesk.DataManagement;
using Autodesk.Oss;
using Autodesk.Oss.Model;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class DMTools
{
    [McpServerTool, Description("Upload the file to Autodesk Docs")]
    public static async Task<string> UploadFileToDocs(
        [Description("File path to upload")] string localFilePath,
        [Description("Data Management API Hub Id used to upload the file to")] string hubId,
        [Description("Data Management API Project Id used to upload the file to")] string projectId, 
        [Description("Folder id used to upload the file to")] string folderId = null)
    {
        try
        {
            // Validate input parameters
            if (string.IsNullOrEmpty(localFilePath))
                throw new ArgumentException("Local file path cannot be null or empty.", nameof(localFilePath));

            if (string.IsNullOrEmpty(projectId))
                throw new ArgumentException("Project ID cannot be null or empty.", nameof(projectId));

            if (!File.Exists(localFilePath))
                throw new FileNotFoundException($"File not found: {localFilePath}");

            IApsConfiguration apsConfiguration = new ApsConfiguration(AdskEnvironment.Prd);
            var sdkManager = SdkManagerBuilder.Create().Add(apsConfiguration).Build();

            StaticAuthenticationProvider staticAuthenticationProvider = new StaticAuthenticationProvider(Global.AccessToken);

            var _dataManagementClient = new DataManagementClient(sdkManager, staticAuthenticationProvider);
            var _ossClient = new OssClient(sdkManager, staticAuthenticationProvider);

            var topFolder =  await _dataManagementClient.GetProjectTopFoldersAsync(hubId:hubId, projectId:projectId, accessToken:Global.AccessToken);
            folderId = folderId ?? topFolder.Data.FirstOrDefault()?.Id;

            // Get file information
            FileInfo fileInfo = new FileInfo(localFilePath);
            string fileName = fileInfo.Name;
            long fileSize = fileInfo.Length;

            // Create storage location
            StoragePayloadDataRelationshipsTarget relationshipTarget = new StoragePayloadDataRelationshipsTarget
            {
                Data = new StoragePayloadDataRelationshipsTargetData
                {
                    Type = TypeFolderItemsForStorage.Folders,
                    Id = folderId
                }
            };

            StoragePayloadDataRelationships relationships = new StoragePayloadDataRelationships
            {
                Target = relationshipTarget
            };

            StoragePayloadDataAttributes attributes = new StoragePayloadDataAttributes
            {
                Name = fileName
            };

            StoragePayload createStorage = new StoragePayload
            {
                Data = new StoragePayloadData
                {
                    Type = TypeObject.Objects,
                    Attributes = attributes,
                    Relationships = relationships
                }
            };

            Storage storageLocation = await _dataManagementClient.CreateStorageAsync(
                projectId: projectId,
                accessToken: Global.AccessToken,
                storagePayload: createStorage
            );

            // Upload file to the storage location
            var storageUrlList = storageLocation.Data.Id.Split("/").ToArray();
            if(storageUrlList.Length != 2)
            {
                throw new ArgumentException("Invalid storage URL format.");
            }

            var bucketKey = storageUrlList[0].Split(":").LastOrDefault<string>();
            var objectKey = storageUrlList[1];

            using (FileStream fileStream = File.OpenRead(localFilePath))
            {
                await _ossClient.UploadObjectAsync(
                    bucketKey: bucketKey,
                    objectKey: objectKey,
                    sourceToUpload: fileStream,
                    accessToken: Global.AccessToken
                );
            }

            ItemPayload itemPayload = new ItemPayload()
            {
                Jsonapi = new JsonApiVersion()
                {
                    VarVersion = JsonApiVersionValue._10
                },
                Data = new ItemPayloadData()
                {
                    Type = TypeItem.Items,
                    Attributes = new ItemPayloadDataAttributes()
                    {
                        DisplayName = fileName,
                        Extension = new ItemPayloadDataAttributesExtension()
                        {
                            Type = "items:autodesk.bim360:File",
                            VarVersion = "1.0"
                        }
                    },
                    Relationships = new ItemPayloadDataRelationships()
                    {
                        Tip = new ItemPayloadDataRelationshipsTip()
                        {
                            Data = new ItemPayloadDataRelationshipsTipData()
                            {
                                Type = TypeVersion.Versions,
                                Id = "1"
                            }
                        },
                        Parent = new ItemPayloadDataRelationshipsParent()
                        {
                            Data = new ItemPayloadDataRelationshipsParentData()
                            {
                                Type = TypeFolder.Folders,
                                Id = folderId,
                            }
                        }
                    }
                },
                Included = new List<ItemPayloadIncluded>()
            {
                new ItemPayloadIncluded()
                {
                    Type = TypeVersion.Versions,
                    Id = "1",
                    Attributes = new ItemPayloadIncludedAttributes()
                    {
                        Name = fileName,
                        Extension = new ItemPayloadIncludedAttributesExtension()
                        {
                            Type = "versions:autodesk.bim360:File",
                            VarVersion = "1.0"
                        }
                    },
                    Relationships = new ItemPayloadIncludedRelationships()
                    {
                        Storage = new ItemPayloadIncludedRelationshipsStorage()
                        {
                            Data = new ItemPayloadIncludedRelationshipsStorageData()
                            {
                                Type = TypeObject.Objects,
                                Id = storageLocation.Data.Id
                            }
                        }
                    }
                }
            }
            };

            // TBD, Create the item only, will support new version later 
            CreatedItem createdItem = await _dataManagementClient.CreateItemAsync(
                projectId: projectId,
                accessToken: Global.AccessToken,
                itemPayload: itemPayload
            );

            JObject jsonData = JObject.FromObject(createdItem.Data);
            return jsonData.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to upload file to ACC: {ex.Message}", ex);
        }
    }
}
