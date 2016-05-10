using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Hearthstone_Deck_Tracker.HsReplay.API
{
	internal class Web
	{
		public static async Task<HttpWebResponse> GetAsync(string url, params Header[] headers)
			=> await SendWebRequestAsync(CreateRequest(url, "GET"), null, false, headers);

		public static async Task<HttpWebResponse> PostAsync(string url, string data, bool gzip, params Header[] headers)
			=> await SendWebRequestAsync(CreateRequest(url, "POST"), data, gzip, headers);

		public static async Task<HttpWebResponse> PutAsync(string url, string data, params Header[] headers) 
			=> await SendWebRequestAsync(CreateRequest(url, "PUT"), data, false, headers);

		private static async Task<HttpWebResponse> SendWebRequestAsync(HttpWebRequest request, string data, bool gzip, params Header[] headers)
		{
			foreach(var header in headers)
				request.Headers.Add(header.Name, header.Value);
			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			if(data == null)
				return (HttpWebResponse)await request.GetResponseAsync();
			using(var stream = await request.GetRequestStreamAsync())
			{
				if(gzip)
				{
					request.Headers.Add(HttpRequestHeader.ContentEncoding, "gzip");
					using(var zipStream = new GZipStream(stream, CompressionMode.Compress))
						zipStream.Write(Encoding.UTF8.GetBytes(data), 0, data.Length);
				}
				else
					stream.Write(Encoding.UTF8.GetBytes(data), 0, data.Length);
			}
			return (HttpWebResponse)await request.GetResponseAsync();
		}

		private static HttpWebRequest CreateRequest(string url, string method)
		{
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.ContentType = "text/plain";
			request.Accept = "application/json";
			request.Method = method;
			return request;
		}
	}
}