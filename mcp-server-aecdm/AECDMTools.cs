using ModelContextProtocol.Server;
using System.ComponentModel;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL;
using Newtonsoft.Json.Linq;


using Autodesk.Data;
using Autodesk.Data.DataModels;
using Autodesk.GeometryUtilities.ConversionAPI;
using Autodesk.SDKManager;
using System.IO;
using Autodesk.DataManagement.Model;
using Autodesk.Data.Interface;
using Autodesk.Data.OpenAPI;
using System.Text;
using System.Numerics;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace mcp_server_aecdm.Tools;

[McpServerToolType]
public static class AECDMTools
{
    private const string BASE_URL = "https://developer-stg.api.autodesk.com/aec/graphql";


	public static async Task<object> Query(GraphQL.GraphQLRequest query, string? regionHeader = null)
	{
		var client = new GraphQLHttpClient(BASE_URL, new NewtonsoftJsonSerializer());
		client.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Global.AccessToken);
		if (!String.IsNullOrWhiteSpace(regionHeader))
			client.HttpClient.DefaultRequestHeaders.Add("region", regionHeader);
		var response = await client.SendQueryAsync<object>(query);

		if (response.Data == null) return response.Errors[0].Message;
		return response.Data;
	}

	[McpServerTool, Description("Get the ACC hubs from the user")]
	public static async Task<string> GetHubs()
	{
		var query = new GraphQL.GraphQLRequest
		{
			Query = @"
                query {
                    hubs {
                        pagination {
                            cursor
                        }
                        results {
                            id
                            name
							alternativeIdentifiers{
							  dataManagementAPIHubId
							}

                        }
                    }
                }",
		};

		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray hubs = (JArray)jsonData.SelectToken("hubs.results");
        List<Hub> hubList = new List<Hub>();
        foreach (var hub in hubs.ToList())
        {
            try
            {
                Hub newHub = new Hub();
                newHub.id = hub.SelectToken("id").ToString();
                newHub.name = hub.SelectToken("name").ToString();
                newHub.dataManagementAPIHubId = hub.SelectToken("alternativeIdentifiers.dataManagementAPIHubId").ToString();
                hubList.Add(newHub);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string hubsString = hubList.Select(hub => hub.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return hubsString;
    }

    [McpServerTool, Description("Get the ACC projects from one hub")]
	public static async Task<string> GetProjects([Description("Hub id, don't use dataManagementAPIHubId")]string hubId)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			    query GetProjects ($hubId: ID!) {
			        projects (hubId: $hubId) {
                        pagination {
                           cursor
                        }
                        results {
                            id
                            name
							alternativeIdentifiers{
							  dataManagementAPIProjectId
							}
                        }
			        }
			    }",
			Variables = new
			{
				hubId = hubId
			}
		};

		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
        JArray projects = (JArray)jsonData.SelectToken("projects.results");

        List<Project> projectList = new List<Project>();
        foreach (var project in projects.ToList())
        {
            try
            {
                var newProject = new Project();
                newProject.id = project.SelectToken("id").ToString();
                newProject.name = project.SelectToken("name").ToString();
                newProject.dataManagementAPIProjectId = project.SelectToken("alternativeIdentifiers.dataManagementAPIProjectId").ToString();
                projectList.Add(newProject);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        string projectsString = projectList.Select(project => project.ToString()).Aggregate((a, b) => $"{a}, {b}");
        return projectsString;
    }

	[McpServerTool, Description("Get the Designs/Models/ElementGroups from one project")]
	public static async Task<string> GetElementGroupsByProject([Description("Project id, don't use dataManagementAPIProjectId")]string projectId)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			    query GetElementGroupsByProject ($projectId: ID!) {
			        elementGroupsByProject(projectId: $projectId) {
			            results{
			                id
			                name
											alternativeIdentifiers{
												fileVersionUrn
											}
			            }
			        }
			    }",
			Variables = new
			{
				projectId = projectId
			}
		};
		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray elementGroups = (JArray)jsonData.SelectToken("elementGroupsByProject.results");

		List<ElementGroup> elementGroupsList = new List<ElementGroup>();
		foreach (var elementGroup in elementGroups.ToList())
		{
			try
			{
				ElementGroup newElementGroup = new ElementGroup();
				newElementGroup.id = elementGroup.SelectToken("id").ToString();
				newElementGroup.name = elementGroup.SelectToken("name").ToString();
				newElementGroup.fileVersionUrn = elementGroup.SelectToken("alternativeIdentifiers.fileVersionUrn").ToString();
				elementGroupsList.Add(newElementGroup);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
		string elementGroupsString = elementGroupsList.Select(eg => $"name: {eg.name}, id: {eg.id}, fileVersionUrn: {eg.fileVersionUrn}").Aggregate((a, b) => $"{a}, {b}");
		return elementGroupsString;
	}

	[McpServerTool, Description("Get the Elements from the ElementGroup/Design using a category filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Ceilings, Electrical Equipment")]
	public static async Task<string> GetElementsByElementGroupWithCategoryFilter([Description("ElementGroup id, not the file version urn")] string elementGroupId, [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string category)
	{
		var query = new GraphQLRequest
		{
			Query = @"
			query GetElementsByElementGroupWithFilter ($elementGroupId: ID!, $filter: String!) {
			  elementsByElementGroup(elementGroupId: $elementGroupId, pagination: {limit:500}, filter: {query:$filter}) {
			    results{
			      id
			      name
			      properties {
			        results {
			            name
			            value
			        }
			      }
			    }
			  }
			}",
			Variables = new
			{
				elementGroupId = elementGroupId,
                filter = $"'property.name.category'=='{category}' and 'property.name.Element Context'=='Instance'"
			}
		};
		object data = await Query(query);

		JObject jsonData = JObject.FromObject(data);
		JArray elements = (JArray)jsonData.SelectToken("elementsByElementGroup.results");

		List<Element> elementsList = new List<Element>();
		//Loop through elements 
		foreach (var element in elements.ToList())
		{
			try
			{
				Element newElement = new Element();
				newElement.id = element.SelectToken("id").ToString();
				newElement.name = element.SelectToken("name").ToString();
				newElement.properties = new List<Property>();
				JArray properties = (JArray)element.SelectToken("properties.results");
				foreach (JToken property in properties.ToList())
				{
					try
					{
						newElement.properties.Add(new Property
						{
							name = property.SelectToken("name").ToString(),
							value = property.SelectToken("value").ToString()
						});
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
					}
				}
				elementsList.Add(newElement);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		string elementsString = elementsList.Select(el => el.ToString()).Aggregate((a,b)=>$"{a}; {b}");
        Console.WriteLine(elementsString);
        return elementsString;
	}


    [McpServerTool, Description("Get the Elements from the ElementGroup/Design using a filter. The filter is a string that will be used in the GraphQL query. For example: 'property.name.category'=='Walls' and 'property.name.Element Context'=='Instance'")]
    public static async Task<string> ExportIfcForElementGroup(
        [Description("ElementGroup id, not the file version urn")] string elementGroupId,
        [Description("Category name to be used as filter. Possible categories are: Walls, Windows, Floors, Doors, Furniture, Roofs, Ceilings, Electrical Equipment, Structural Framing, Structural Columns, Structural Rebar")] string category,
        [Description("File name of this exported IFC file.")] string? fileName = null)
    {
        string path = string.Empty;
        try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = await aecdmService.GetAllElementsByElementGroupParallelAsync(elementGroupId);
            var newElements = elements.ToList().Where(el =>
            {
                var propCategory = el.Properties.Results.Where(prop => prop.Name == "Revit Category Type Id").ToList();
                var propContext = el.Properties.Results.Where(prop => prop.Name == "Element Context").ToList();
                return (propCategory.Count > 0 && propContext.Count > 0 && propContext[0].Value == "Instance" && propCategory[0].Value == category);
            }).ToList();

            elementGroup.AddAECDMElements(newElements);
            path = await elementGroup.ConvertToIfc(ifcFileId: fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nApplication failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            path = ex.Message;
        }

        return path;
    }


    [McpServerTool, Description("Export Ifc file for the selected element")]
    public static async Task<string> ExportIfcForElements(
		[Description("Array of element ids that will be exported to IFC!")] string[] elementIds, 
		[Description("File name of this exported IFC file.")] string? fileName = null)
    {
		string path = string.Empty;
		try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = new List<AECDMElement>();
			var tasks = elementIds.ToList().Select(async (item) =>
			{
				var element = await aecdmService.GetElementData(item);
				elements.Add(element.Data.ElementAtTip);
			});
			await Task.WhenAll(tasks);

			// Alternatively, add elements in a batch
			elementGroup.AddAECDMElements(elements);
            path = await elementGroup.ConvertToIfc( ifcFileId:fileName );
        }
        catch (Exception ex)
        {
			Console.WriteLine($"\nApplication failed with error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
            path = ex.Message;
        }

        return path;
    }




    [McpServerTool, Description("Perform accurate clash detection analysis between selected building elements")]
    public static async Task<string> ClashDetectForElements(
        [Description("Array of element ids to analyze for clashes")] string[] elementIds,
        [Description("Minimum clash volume threshold in cubic units (default: 0.01 - higher values reduce false positives)")] double clashThreshold = 0.01,
        [Description("Clearance distance for near-miss detection in units (default: 0.1)")] double clearanceThreshold = 0.1,
        [Description("Enable parallel processing for large datasets (default: true)")] bool enableParallelProcessing = true)
    {
        var clashResults = new StringBuilder();
        var meshSummary = new StringBuilder();
        var performanceMetrics = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var elementGroup = Autodesk.Data.DataModels.ElementGroup.Create(Global.SDKClient);
            var aecdmService = new AECDMService(Global.SDKClient);

            List<AECDMElement> elements = new List<AECDMElement>();
            var tasks = elementIds.ToList().Select(async (item) =>
            {
                var element = await aecdmService.GetElementData(item);
                elements.Add(element.Data.ElementAtTip);
            });
            await Task.WhenAll(tasks);

            // Add elements to element group
            elementGroup.AddAECDMElements(elements);
            
            // Get ElementGeometriesAsMesh
            var ElementGeomMap = await elementGroup.GetElementGeometriesAsMeshAsync().ConfigureAwait(false);

            // Create element bounding boxes for clash detection analysis
            var elementCollisionData = new List<ElementBoundingBox>();
            var geometryProcessingTime = stopwatch.ElapsedMilliseconds;
            
            // Process each element and create bounding boxes
            foreach (KeyValuePair<Autodesk.Data.DataModels.Element, IEnumerable<ElementGeometry>> kv in ElementGeomMap)
            {
                var element = kv.Key;
                var meshObjList = kv.Value;
                
                meshSummary.AppendLine($"Element ID: {element.Id} has {meshObjList.Count()} meshes");
                
                foreach (var meshObj in meshObjList)
                {
                    if (meshObj is Autodesk.Data.DataModels.MeshGeometry meshGeometry && meshGeometry.Mesh != null)
                    {
                        meshSummary.AppendLine($"  - Mesh Vertices Count: {meshGeometry.Mesh.Vertices.Count}");
                        
                        try
                        {
                            // Create bounding box from mesh vertices with enhanced properties
                            var boundingBox = CreateEnhancedBoundingBoxFromMesh(meshGeometry.Mesh);
                            if (boundingBox != null)
                            {
                                boundingBox.ElementId = element.Id;
                                boundingBox.ElementName = GetElementName(element);
                                boundingBox.AdaptiveTolerance = CalculateAdaptiveTolerance(boundingBox);
                                elementCollisionData.Add(boundingBox);
                                
                                meshSummary.AppendLine($"  - Bounding Box: ({boundingBox.MinX:F2}, {boundingBox.MinY:F2}, {boundingBox.MinZ:F2}) to ({boundingBox.MaxX:F2}, {boundingBox.MaxY:F2}, {boundingBox.MaxZ:F2})");
                                meshSummary.AppendLine($"  - Volume: {boundingBox.Volume:F6}, Adaptive Tolerance: {boundingBox.AdaptiveTolerance:F6}");
                            }
                        }
                        catch (Exception meshEx)
                        {
                            meshSummary.AppendLine($"  - Error creating bounding box: {meshEx.Message}");
                        }
                    }
                    else
                    {
                        meshSummary.AppendLine($"  - Element ID: {element.Id} has no valid geometry.");
                    }
                }
            }

            // Create spatial index for optimized collision detection
            var spatialIndex = new SpatialIndex(elementCollisionData);
            var indexingTime = stopwatch.ElapsedMilliseconds - geometryProcessingTime;
            
            // Perform enhanced clash detection with spatial optimization
            var (clashes, clearances, detectionTime) = await PerformEnhancedClashDetection(
                elementCollisionData, spatialIndex, clashThreshold, clearanceThreshold, enableParallelProcessing);
            
            // Generate results
            clashResults.AppendLine("=== ENHANCED CLASH DETECTION RESULTS ===");
            clashResults.AppendLine($"Total elements analyzed: {elementCollisionData.Count}");
            clashResults.AppendLine($"Clash threshold: {clashThreshold} cubic units");
            clashResults.AppendLine($"Clearance threshold: {clearanceThreshold} units");
            clashResults.AppendLine($"Parallel processing: {(enableParallelProcessing ? "Enabled" : "Disabled")}");
            clashResults.AppendLine();

            var clashCount = clashes.Count;
            var clearanceCount = clearances.Count;

            // Display clashes
            foreach (var clash in clashes.OrderByDescending(c => c.IntersectionVolume))
            {
                var clashNum = clashes.IndexOf(clash) + 1;
                clashResults.AppendLine($"CLASH #{clashNum} - {clash.Severity}:");
                clashResults.AppendLine($"  Element 1: {clash.Element1Name} (ID: {clash.Element1Id})");
                clashResults.AppendLine($"  Element 2: {clash.Element2Name} (ID: {clash.Element2Id})");
                clashResults.AppendLine($"  Details:");
                clashResults.AppendLine($"    - Type: {clash.ClashType}");
                clashResults.AppendLine($"    - Severity: {clash.Severity}");
                clashResults.AppendLine($"    - Intersection Volume: {clash.IntersectionVolume:F6} cubic units");
                clashResults.AppendLine($"    - Element 1 Overlap: {clash.Element1VolumePercent:F2}% of its volume");
                clashResults.AppendLine($"    - Element 2 Overlap: {clash.Element2VolumePercent:F2}% of its volume");
                if (clash.ContactPoints.Any())
                {
                    clashResults.AppendLine($"    - Intersection Center: ({clash.ContactPoints[0].X:F3}, {clash.ContactPoints[0].Y:F3}, {clash.ContactPoints[0].Z:F3})");
                }
                clashResults.AppendLine();
            }
            
            // Display clearance warnings
            if (clearanceCount > 0)
            {
                clashResults.AppendLine("=== CLEARANCE WARNINGS (NEAR MISSES) ===");
                foreach (var clearance in clearances.OrderBy(c => c.MinimumDistance))
                {
                    var clearanceNum = clearances.IndexOf(clearance) + 1;
                    clashResults.AppendLine($"CLEARANCE WARNING #{clearanceNum}:");
                    clashResults.AppendLine($"  Element 1: {clearance.Element1Name} (ID: {clearance.Element1Id})");
                    clashResults.AppendLine($"  Element 2: {clearance.Element2Name} (ID: {clearance.Element2Id})");
                    clashResults.AppendLine($"  Minimum Distance: {clearance.MinimumDistance:F3} units");
                    clashResults.AppendLine($"  Risk Level: {clearance.RiskLevel}");
                    clashResults.AppendLine();
                }
            }

            // Summary
            if (clashCount == 0)
            {
                clashResults.AppendLine("✅ No clashes detected between the analyzed elements.");
            }
            else
            {
                clashResults.AppendLine($"⚠️  Found {clashCount} clashes using optimized spatial detection.");
            }
            
            if (clearanceCount > 0)
            {
                clashResults.AppendLine($"⚠️  Found {clearanceCount} clearance warnings (elements too close).");
            }
            
            // Performance metrics
            performanceMetrics.AppendLine("=== PERFORMANCE METRICS ===");
            performanceMetrics.AppendLine($"Geometry Processing Time: {geometryProcessingTime} ms");
            performanceMetrics.AppendLine($"Spatial Indexing Time: {indexingTime} ms");
            performanceMetrics.AppendLine($"Collision Detection Time: {detectionTime} ms");
            performanceMetrics.AppendLine($"Total Processing Time: {stopwatch.ElapsedMilliseconds} ms");
            performanceMetrics.AppendLine($"Elements/Second: {(elementCollisionData.Count * 1000.0 / Math.Max(stopwatch.ElapsedMilliseconds, 1)):F2}");

            // No cleanup needed for managed clash detection analysis
        }
        catch (Exception ex)
        {
            var errorMessage = $"Application failed with error: {ex.Message}\nStack trace: {ex.StackTrace}";
            Console.WriteLine($"\n{errorMessage}");
            return $"Error during clash detection analysis: {ex.Message}";
        }

        var finalResult = new StringBuilder();
        finalResult.AppendLine("=== ENHANCED CLASH DETECTION ANALYSIS COMPLETED ===");
        finalResult.AppendLine();
        finalResult.AppendLine("ELEMENT GEOMETRY SUMMARY:");
        finalResult.AppendLine(meshSummary.ToString());
        finalResult.AppendLine(clashResults.ToString());
        finalResult.AppendLine(performanceMetrics.ToString());
        
        return finalResult.ToString();
    }

    /// <summary>
    /// Creates an enhanced bounding box from an Autodesk mesh for clash detection analysis
    /// </summary>
    private static ElementBoundingBox? CreateEnhancedBoundingBoxFromMesh(dynamic mesh)
    {
        try
        {
            if (mesh?.Vertices == null || mesh.Vertices.Count < 3)
                return null;

            // Calculate bounding box from vertices
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var vertex in mesh.Vertices)
            {
                float x = (float)vertex.X;
                float y = (float)vertex.Y;
                float z = (float)vertex.Z;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
            }

            // Only ensure minimum size for truly zero-dimension boxes (avoid artificial expansion)
            if (maxX - minX < 0.001f) { minX -= 0.0005f; maxX += 0.0005f; }
            if (maxY - minY < 0.001f) { minY -= 0.0005f; maxY += 0.0005f; }
            if (maxZ - minZ < 0.001f) { minZ -= 0.0005f; maxZ += 0.0005f; }

            return new ElementBoundingBox
            {
                MinX = minX,
                MinY = minY,
                MinZ = minZ,
                MaxX = maxX,
                MaxY = maxY,
                MaxZ = maxZ
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating bounding box: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the element name from the element, with fallback to ID
    /// </summary>
    private static string GetElementName(Autodesk.Data.DataModels.Element element)
    {
        try
        {
            // Try to get a meaningful name from element properties or use the ID
            return !string.IsNullOrEmpty(element.Name) ? element.Name : $"Element_{element.Id}";
        }
        catch
        {
            return $"Element_{element.Id}";
        }
    }

    /// <summary>
    /// Calculates adaptive tolerance based on element size
    /// </summary>
    private static float CalculateAdaptiveTolerance(ElementBoundingBox element)
    {
        var maxDimension = Math.Max(element.MaxX - element.MinX, 
                                  Math.Max(element.MaxY - element.MinY, element.MaxZ - element.MinZ));
        // Use 0.1% of the largest dimension as tolerance, with minimum and maximum bounds
        return (float)Math.Max(0.001, Math.Min(0.1, maxDimension * 0.001));
    }

    /// <summary>
    /// Performs enhanced clash detection with spatial optimization and parallel processing
    /// </summary>
    private static async Task<(List<EnhancedClashInfo> clashes, List<ClearanceInfo> clearances, long detectionTime)> 
        PerformEnhancedClashDetection(List<ElementBoundingBox> elements, SpatialIndex spatialIndex, 
        double clashThreshold, double clearanceThreshold, bool enableParallel)
    {
        var stopwatch = Stopwatch.StartNew();
        var clashes = new ConcurrentBag<EnhancedClashInfo>();
        var clearances = new ConcurrentBag<ClearanceInfo>();

        if (enableParallel)
        {
            await Task.Run(() =>
            {
                Parallel.For(0, elements.Count, i =>
                {
                    var element1 = elements[i];
                    var candidates = spatialIndex.GetNearbyElements(element1);
                    
                    foreach (var element2 in candidates.Where(e => string.Compare(e.ElementId, element1.ElementId, StringComparison.Ordinal) > 0))
                    {
                        var result = AnalyzeElementPair(element1, element2, clashThreshold, clearanceThreshold);
                        if (result.clash != null) clashes.Add(result.clash);
                        if (result.clearance != null) clearances.Add(result.clearance);
                    }
                });
            });
        }
        else
        {
            for (int i = 0; i < elements.Count; i++)
            {
                var element1 = elements[i];
                var candidates = spatialIndex.GetNearbyElements(element1);
                
                foreach (var element2 in candidates.Where(e => string.Compare(e.ElementId, element1.ElementId, StringComparison.Ordinal) > 0))
                {
                    var result = AnalyzeElementPair(element1, element2, clashThreshold, clearanceThreshold);
                    if (result.clash != null) clashes.Add(result.clash);
                    if (result.clearance != null) clearances.Add(result.clearance);
                }
            }
        }

        return (clashes.ToList(), clearances.ToList(), stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Analyzes a pair of elements for clashes and clearance issues
    /// </summary>
    private static (EnhancedClashInfo? clash, ClearanceInfo? clearance) AnalyzeElementPair(
        ElementBoundingBox element1, ElementBoundingBox element2, double clashThreshold, double clearanceThreshold)
    {
        try
        {
            // Use adaptive tolerance for more accurate detection
            var tolerance = Math.Max(element1.AdaptiveTolerance, element2.AdaptiveTolerance);
            
            // Check for AABB overlap with adaptive tolerance
            bool overlapsX = (element1.MaxX - tolerance) > element2.MinX && (element2.MaxX - tolerance) > element1.MinX;
            bool overlapsY = (element1.MaxY - tolerance) > element2.MinY && (element2.MaxY - tolerance) > element1.MinY;
            bool overlapsZ = (element1.MaxZ - tolerance) > element2.MinZ && (element2.MaxZ - tolerance) > element1.MinZ;
            
            if (overlapsX && overlapsY && overlapsZ)
            {
                // Calculate intersection details
                float intersectionX = Math.Min(element1.MaxX, element2.MaxX) - Math.Max(element1.MinX, element2.MinX);
                float intersectionY = Math.Min(element1.MaxY, element2.MaxY) - Math.Max(element1.MinY, element2.MinY);
                float intersectionZ = Math.Min(element1.MaxZ, element2.MaxZ) - Math.Max(element1.MinZ, element2.MinZ);
                
                if (intersectionX > tolerance && intersectionY > tolerance && intersectionZ > tolerance)
                {
                    double intersectionVolume = intersectionX * intersectionY * intersectionZ;
                    
                    if (intersectionVolume >= clashThreshold)
                    {
                        var centerX = (Math.Max(element1.MinX, element2.MinX) + Math.Min(element1.MaxX, element2.MaxX)) / 2;
                        var centerY = (Math.Max(element1.MinY, element2.MinY) + Math.Min(element1.MaxY, element2.MaxY)) / 2;
                        var centerZ = (Math.Max(element1.MinZ, element2.MinZ) + Math.Min(element1.MaxZ, element2.MaxZ)) / 2;
                        
                        double element1VolPercent = (intersectionVolume / element1.Volume) * 100;
                        double element2VolPercent = (intersectionVolume / element2.Volume) * 100;
                        
                        var clash = new EnhancedClashInfo
                        {
                            Element1Id = element1.ElementId,
                            Element1Name = element1.ElementName,
                            Element2Id = element2.ElementId,
                            Element2Name = element2.ElementName,
                            ClashType = ClassifyClashType(intersectionVolume, element1VolPercent, element2VolPercent),
                            Severity = ClassifyClashSeverity(intersectionVolume, element1VolPercent, element2VolPercent),
                            IntersectionVolume = intersectionVolume,
                            ContactPoints = new List<Vector3> { new Vector3(centerX, centerY, centerZ) },
                            Element1VolumePercent = element1VolPercent,
                            Element2VolumePercent = element2VolPercent
                        };
                        
                        return (clash, null);
                    }
                }
            }
            
            // Check for clearance issues (near misses)
            var minDistance = CalculateMinimumDistance(element1, element2);
            if (minDistance <= clearanceThreshold && minDistance > 0)
            {
                var clearance = new ClearanceInfo
                {
                    Element1Id = element1.ElementId,
                    Element1Name = element1.ElementName,
                    Element2Id = element2.ElementId,
                    Element2Name = element2.ElementName,
                    MinimumDistance = minDistance,
                    RiskLevel = ClassifyRiskLevel(minDistance, clearanceThreshold)
                };
                
                return (null, clearance);
            }
            
            return (null, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing element pair: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Calculates minimum distance between two bounding boxes
    /// </summary>
    private static double CalculateMinimumDistance(ElementBoundingBox box1, ElementBoundingBox box2)
    {
        var dx = Math.Max(0, Math.Max(box1.MinX - box2.MaxX, box2.MinX - box1.MaxX));
        var dy = Math.Max(0, Math.Max(box1.MinY - box2.MaxY, box2.MinY - box1.MaxY));
        var dz = Math.Max(0, Math.Max(box1.MinZ - box2.MaxZ, box2.MinZ - box1.MaxZ));
        
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Classifies clash type based on intersection characteristics
    /// </summary>
    private static string ClassifyClashType(double intersectionVolume, double vol1Percent, double vol2Percent)
    {
        if (Math.Max(vol1Percent, vol2Percent) > 50)
            return "Major Penetration";
        else if (intersectionVolume > 1.0)
            return "Significant Overlap";
        else if (intersectionVolume > 0.1)
            return "Moderate Intersection";
        else
            return "Minor Overlap";
    }

    /// <summary>
    /// Classifies clash severity
    /// </summary>
    private static string ClassifyClashSeverity(double intersectionVolume, double vol1Percent, double vol2Percent)
    {
        var maxPercent = Math.Max(vol1Percent, vol2Percent);
        
        if (maxPercent > 75 || intersectionVolume > 10)
            return "CRITICAL";
        else if (maxPercent > 25 || intersectionVolume > 1)
            return "HIGH";
        else if (maxPercent > 5 || intersectionVolume > 0.1)
            return "MEDIUM";
        else
            return "LOW";
    }

    /// <summary>
    /// Classifies risk level for clearance issues
    /// </summary>
    private static string ClassifyRiskLevel(double distance, double threshold)
    {
        var ratio = distance / threshold;
        
        if (ratio < 0.2)
            return "HIGH RISK";
        else if (ratio < 0.5)
            return "MEDIUM RISK";
        else
            return "LOW RISK";
    }


}

/// <summary>
/// Represents an enhanced bounding box for an element used in clash detection analysis
/// </summary>
internal class ElementBoundingBox
{
    public string ElementId { get; set; } = string.Empty;
    public string ElementName { get; set; } = string.Empty;
    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }
    public float AdaptiveTolerance { get; set; } = 0.001f;

    public double Volume => Math.Abs((MaxX - MinX) * (MaxY - MinY) * (MaxZ - MinZ));
    
    public Vector3 Center => new Vector3((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);
    
    public double MaxDimension => Math.Max(MaxX - MinX, Math.Max(MaxY - MinY, MaxZ - MinZ));
    
    /// <summary>
    /// Checks if this bounding box overlaps with another bounding box
    /// </summary>
    public bool OverlapsWith(ElementBoundingBox other, float tolerance = 0.001f)
    {
        return (MaxX - tolerance) > other.MinX && (other.MaxX - tolerance) > MinX &&
               (MaxY - tolerance) > other.MinY && (other.MaxY - tolerance) > MinY &&
               (MaxZ - tolerance) > other.MinZ && (other.MaxZ - tolerance) > MinZ;
    }
}

/// <summary>
/// Contains detailed information about a detected element clash
/// </summary>
internal class ClashInfo
{
    public string ClashType { get; set; } = string.Empty;
    public double IntersectionVolume { get; set; }
    public List<Vector3> ContactPoints { get; set; } = new List<Vector3>();
    public double Element1VolumePercent { get; set; }
    public double Element2VolumePercent { get; set; }
}

/// <summary>
/// Enhanced clash information with additional classification and metadata
/// </summary>
internal class EnhancedClashInfo
{
    public string Element1Id { get; set; } = string.Empty;
    public string Element1Name { get; set; } = string.Empty;
    public string Element2Id { get; set; } = string.Empty;
    public string Element2Name { get; set; } = string.Empty;
    public string ClashType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public double IntersectionVolume { get; set; }
    public List<Vector3> ContactPoints { get; set; } = new List<Vector3>();
    public double Element1VolumePercent { get; set; }
    public double Element2VolumePercent { get; set; }
}

/// <summary>
/// Information about clearance issues (near misses)
/// </summary>
internal class ClearanceInfo
{
    public string Element1Id { get; set; } = string.Empty;
    public string Element1Name { get; set; } = string.Empty;
    public string Element2Id { get; set; } = string.Empty;
    public string Element2Name { get; set; } = string.Empty;
    public double MinimumDistance { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
}

/// <summary>
/// Spatial index for optimized collision detection using grid-based partitioning
/// </summary>
internal class SpatialIndex
{
    private readonly Dictionary<string, List<ElementBoundingBox>> _spatialGrid;
    private readonly float _cellSize;
    private readonly float _minX, _minY, _minZ;
    private readonly int _gridSizeX, _gridSizeY, _gridSizeZ;

    public SpatialIndex(List<ElementBoundingBox> elements, float cellSize = 10.0f)
    {
        _cellSize = cellSize;
        _spatialGrid = new Dictionary<string, List<ElementBoundingBox>>();

        if (elements.Count == 0) return;

        // Calculate bounding box of all elements
        _minX = elements.Min(e => e.MinX);
        _minY = elements.Min(e => e.MinY);
        _minZ = elements.Min(e => e.MinZ);
        var maxX = elements.Max(e => e.MaxX);
        var maxY = elements.Max(e => e.MaxY);
        var maxZ = elements.Max(e => e.MaxZ);

        // Calculate grid dimensions
        _gridSizeX = (int)Math.Ceiling((maxX - _minX) / _cellSize) + 1;
        _gridSizeY = (int)Math.Ceiling((maxY - _minY) / _cellSize) + 1;
        _gridSizeZ = (int)Math.Ceiling((maxZ - _minZ) / _cellSize) + 1;

        // Populate spatial grid
        foreach (var element in elements)
        {
            var cells = GetCellsForElement(element);
            foreach (var cell in cells)
            {
                if (!_spatialGrid.ContainsKey(cell))
                    _spatialGrid[cell] = new List<ElementBoundingBox>();
                _spatialGrid[cell].Add(element);
            }
        }
    }

    /// <summary>
    /// Gets nearby elements that could potentially clash with the given element
    /// </summary>
    public List<ElementBoundingBox> GetNearbyElements(ElementBoundingBox element)
    {
        var nearbyElements = new HashSet<ElementBoundingBox>();
        var cells = GetCellsForElement(element);

        foreach (var cell in cells)
        {
            if (_spatialGrid.ContainsKey(cell))
            {
                foreach (var candidate in _spatialGrid[cell])
                {
                    if (candidate.ElementId != element.ElementId)
                        nearbyElements.Add(candidate);
                }
            }
        }

        return nearbyElements.ToList();
    }

    /// <summary>
    /// Gets all grid cells that an element spans
    /// </summary>
    private List<string> GetCellsForElement(ElementBoundingBox element)
    {
        var cells = new List<string>();

        var minCellX = (int)((element.MinX - _minX) / _cellSize);
        var maxCellX = (int)((element.MaxX - _minX) / _cellSize);
        var minCellY = (int)((element.MinY - _minY) / _cellSize);
        var maxCellY = (int)((element.MaxY - _minY) / _cellSize);
        var minCellZ = (int)((element.MinZ - _minZ) / _cellSize);
        var maxCellZ = (int)((element.MaxZ - _minZ) / _cellSize);

        // Clamp to grid bounds
        minCellX = Math.Max(0, Math.Min(minCellX, _gridSizeX - 1));
        maxCellX = Math.Max(0, Math.Min(maxCellX, _gridSizeX - 1));
        minCellY = Math.Max(0, Math.Min(minCellY, _gridSizeY - 1));
        maxCellY = Math.Max(0, Math.Min(maxCellY, _gridSizeY - 1));
        minCellZ = Math.Max(0, Math.Min(minCellZ, _gridSizeZ - 1));
        maxCellZ = Math.Max(0, Math.Min(maxCellZ, _gridSizeZ - 1));

        for (int x = minCellX; x <= maxCellX; x++)
        {
            for (int y = minCellY; y <= maxCellY; y++)
            {
                for (int z = minCellZ; z <= maxCellZ; z++)
                {
                    cells.Add($"{x},{y},{z}");
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Gets statistics about the spatial index
    /// </summary>
    public (int totalCells, int occupiedCells, double averageElementsPerCell) GetStatistics()
    {
        var totalCells = _gridSizeX * _gridSizeY * _gridSizeZ;
        var occupiedCells = _spatialGrid.Count;
        var averageElementsPerCell = occupiedCells > 0 ? _spatialGrid.Values.Sum(list => list.Count) / (double)occupiedCells : 0;

        return (totalCells, occupiedCells, averageElementsPerCell);
    }
}



internal class ElementGroup
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal string fileVersionUrn { get; set; } = string.Empty;

	public override string ToString()
	{
		return $"id: {id}, name: {name}, fileVersionUrn: {fileVersionUrn}";
	}
}


internal class Hub
{
   internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIHubId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"hubId: {id}, hubName: {name}, dataManagementAPIHubId: {dataManagementAPIHubId}";
    }
}

internal class Project
{
    internal string id { get; set; } = string.Empty;
    internal string name { get; set; } = string.Empty;
    internal string dataManagementAPIProjectId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"projectId: {id}, projectName: {name}, dataManagementAPIProjectId: {dataManagementAPIProjectId}";
    }
}	




internal class Element
{
	internal string id { get; set; } = string.Empty;
	internal string name { get; set; } = string.Empty;
	internal List<Property> properties { get; set; } = new List<Property>();

	public override string ToString()
	{
		var externalIdProp = properties.Find(prop => prop.name == "External ID");
		return $"id: {id}, name: {name}, external id: {externalIdProp?.value ?? "N/A"} ";
	}
}

internal class Property
{
	internal string name { get; set; } = string.Empty;
	internal string value { get; set; } = string.Empty;
	public override string ToString()
	{
		return $"name: {name}, value: {value}";
	}
}