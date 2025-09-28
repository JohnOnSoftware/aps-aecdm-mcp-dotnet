using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Web;

namespace mcp_server_aecdm
{
	[McpServerToolType]
	public static class ViewerTool
	{
		[McpServerTool, Description("Show the elements in viewer based on their External Ids")]
		public static async Task<string> HighLightElements([Description("Array of external ids that must be highlighted!")] string[] externalIds)
		{
			if (Global._webSocket != null && Global._webSocket.State == WebSocketState.Open)
			{
				byte[] buffer = Encoding.UTF8.GetBytes(string.Join(",", externalIds));
				await Global._webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			return "Elements highlighted!";
		}

		[McpServerTool, Description("Render one model with Autodesk Viewer.")]
		public static async Task<string> RenderModel([Description("urn used to render the model with Viewer")] string fileVersionUrn)
		{

			//return an html page as string
            string htmlContent = @"
<!DOCTYPE html>
<html>

<head>
  <title>Autodesk Platform Services Claude Viewer Template</title>
  <!-- Autodesk Platform Services Viewer files -->
  <link rel=""stylesheet"" href=""https://developer.api.autodesk.com/modelderivative/v2/viewers/7.*/style.min.css""
        type=""text/css"">
  <script src=""https://developer.api.autodesk.com/modelderivative/v2/viewers/7.*/viewer3D.min.js""></script>
</head>

<body onload=""initAPSViewer()"">
  <div id=""apsViewer""></div>
</body>
<script>

  let viewer = null;
  var _access_token = '" + Global.AccessToken+ @"';
  var _urn = Autodesk.Viewing.toUrlSafeBase64( '"+ fileVersionUrn + @"');

	let socket = new WebSocket('ws://localhost:8081');
  socket.onmessage = function(event) {
      var externalIds = event.data.split(',');
			viewer.model.getExternalIdMapping((externalIdsDictionary) => {
        let dbids = [];
        externalIds.forEach(externalId => {
          let dbid = externalIdsDictionary[externalId];
          if (!!dbid)
            dbids.push(dbid);
        });
        viewer.isolate(dbids);
        viewer.fitToView();
      }, console.log)
  };

  function onLoadFinished() {
    console.log('Model loaded successfully');
  }

  async function initAPSViewer() {
    const options = {
      env: 'AutodeskStaging2',
      accessToken: _access_token,
      isAEC: true
    };

    Autodesk.Viewing.Initializer(options, () => {

      const div = document.getElementById('apsViewer');

			const config = { extensions: ['Autodesk.DocumentBrowser'] }

      viewer = new Autodesk.Viewing.Private.GuiViewer3D(div, config);
      viewer.start();
      viewer.setTheme(""light-theme"");
      Autodesk.Viewing.Document.load(`urn:${_urn}`, doc => {
        var viewables = doc.getRoot().getDefaultGeometry();
        viewer.loadDocumentNode(doc, viewables).then(onLoadFinished);
      });
    });
  }

</script>


</html>";
			// Start a simple HTTP server with error handling
			HttpListener listener = null;
			HttpListener webSocketListener = null;
			
			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add("http://localhost:8082/");
				listener.Start();
				
				Task.Run(() =>
				{
					try
					{
						while (listener.IsListening)
						{
							var context = listener.GetContext();
							var response = context.Response;
							byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
							response.ContentLength64 = buffer.Length;
							response.OutputStream.Write(buffer, 0, buffer.Length);
							response.OutputStream.Close();
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"HTTP Server error: {ex.Message}");
					}
				});
			}
			catch (Exception ex)
			{
				return $"Failed to start HTTP server on port 8082: {ex.Message}. Port may be in use.";
			}

			// Start a WebSocket server with error handling
			try
			{
				webSocketListener = new HttpListener();
				webSocketListener.Prefixes.Add("http://localhost:8081/");
				webSocketListener.Start();

				Task.Run(async () =>
				{
					try
					{
						while (webSocketListener.IsListening)
						{
							var context = await webSocketListener.GetContextAsync();
							if (context.Request.IsWebSocketRequest)
							{
								var webSocketContext = await context.AcceptWebSocketAsync(null);
								Global._webSocket = webSocketContext.WebSocket;
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"WebSocket Server error: {ex.Message}");
					}
				});
			}
			catch (Exception ex)
			{
				listener?.Stop();
				return $"Failed to start WebSocket server on port 8081: {ex.Message}. Port may be in use.";
			}

			// Try multiple methods to open the browser
			string url = "http://localhost:8082/";
			try
			{
                // Method 1: Try with UseShellExecute = true
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return "Viewer has been initialized and browser opened successfully!";
            }
			catch (Exception ex1)
			{
                try
                {
                    // Method 2: Try with cmd /c start
                    var psi2 = new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start {url}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi2);
                    return "Viewer has been initialized and browser opened via cmd!";
                }
                catch (Exception ex2)
                {
                    try
                    {
                        // Method 3: Try direct browser executable
                        var psi3 = new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = url,
                            UseShellExecute = false
                        };
                        Process.Start(psi3);
                        return "Viewer has been initialized and browser opened via explorer!";
                    }
                    catch (Exception ex3)
                    {
                        // All methods failed, return detailed error info
                        return $"Viewer server started on {url} but failed to open browser automatically. " +
                               $"Please open your browser manually and navigate to {url}. " +
                               $"Errors: Method1: {ex1.Message}, Method2: {ex2.Message}, Method3: {ex3.Message}";
                    }
                }
            }
		}
	}
}
