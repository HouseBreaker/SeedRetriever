using System;
using System.Linq;

namespace Shameless.Utils
{
	using System.Diagnostics;
	using System.IO;
	using System.Net;
	using System.Security.Cryptography.X509Certificates;
	using System.Text;
	using System.Xml.Linq;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using SeedRetriever;

	public static class CDNUtils
	{
		public static long GetTitleSize(string titleId)
		{
			// translated from FunKeyCIA
			var cdnUrl = "http://ccs.cdn.c.shop.nintendowifi.net/ccs/download/" + titleId.ToUpper();

			byte[] tmd;

			using (var client = new WebClient())
			{
				tmd = client.DownloadData(cdnUrl + "/tmd");
			}

			const int TikOffset = 0x140;

			var contentCount = Convert.ToInt32(BitConversion.BytesToHex(tmd.Skip(TikOffset + 0x9E).Take(2)), 16);

			long size = 0;

			for (int i = 0; i < contentCount; i++)
			{
				var contentOffset = 0xB04 + 0x30 * i;
				var contentId = BitConversion.BytesToHex(tmd.Skip(contentOffset).Take(4));

				try
				{
					var req = WebRequest.Create(cdnUrl + "/" + contentId);

					using (var resp = req.GetResponse())
					{
						long currentSize;
						if (long.TryParse(resp.Headers.Get("Content-Length"), out currentSize))
						{
							size += currentSize;
						}
					}
				}
				catch (WebException)
				{
				}
			}

			return size;
		}

		public static byte[] RetrieveSeed(string titleId)
		{
			ServicePointManager.ServerCertificateValidationCallback = (sender, x509Certificate, chain, errors) => true;
			const string country = "US";

			using (var client = new WebClient())
			{
				try
				{
					const string CdnUrl = "https://kagiya-ctr.cdn.nintendo.net/title/0x{0}/ext_key?country={1}";
					var seedAsBytes = client.DownloadData(string.Format(CdnUrl, titleId, country));

					return seedAsBytes;

					// var seed = BitConversion.BytesToHex(seedAsBytes);
					// var titleSeedPair = $"{titleId} {seed}{Environment.NewLine}";

					// Console.Write(titleSeedPair);

					// var fileContents = File.ReadAllText("seeds.txt");
					// if (!fileContents.Contains(titleSeedPair))
					// {
					// 	File.AppendAllText("seeds.txt", titleSeedPair);
					// }
				}
				catch (WebException ex)
				{
					var response = (HttpWebResponse)ex.Response;
					var statusCode = (int)response.StatusCode;

					// Console.WriteLine($"{titleId} {statusCode}");
				}

				return null;
			}
		}

		public static TitleData RetrieveTitleData(string[] internalInfo, string titleId, string country, X509Certificate2 certificate)
		{
			var titleData = new TitleData(titleId);

			using (var client = new CertificateWebClient(certificate))
			{
				client.Encoding = Encoding.UTF8;

				try
				{
					// Console.Write(titleId + ": ");
					// internalInfo = RetrieveInternalInfo(titleId, certificate);
					if (internalInfo[0] == null || internalInfo[1] == null)
					{
						// Console.WriteLine("No info");
						return titleData;
					}

					var internalId = internalInfo[0];
					var type = internalInfo[1];

					titleData.InternalId = internalId;
					titleData.Type = type;

					// Console.Write($"{internalId} ({type}) - ");
					// var countries = new[] { "US", "JP", "HK", "TW", "KR", "GB", "DE", "FR", "ES", "NL", "IT" };

					string metadataResponse = null;
					//foreach (var country in countries)
					//{
						try
						{
							client.Headers.Add("Accept", "application/json");
							const string MetadataUrl = "https://samurai.ctr.shop.nintendo.net/samurai/ws/";
							metadataResponse = client.DownloadString(MetadataUrl + country + "/title/" + internalId);

							// metadataResponse = client.DownloadString(MetadataUrl + country + "/titles");
							//break;
						}
						catch (WebException ex)
						{
							// continue
						}
					//}

					if (metadataResponse == null)
					{
						// Console.WriteLine("No info");
						return titleData;

						// 	for (int i = 'A'; i <= 'Z'; i++)
						// 	{
						// 		for (int j = 'A'; j <= 'Z'; j++)
						// 		{
						// 			var country = $"{(char)i}{(char)j}";
						// 			try
						// 			{
						// 				const string metadataUrl = "https://samurai.ctr.shop.nintendo.net/samurai/ws/";
						// 				metadataResponse = client.DownloadString(metadataUrl + country + "/title/" + internalId);

						// 				Console.CursorLeft = Console.CursorLeft == 37 ? Console.CursorLeft + 1 : Console.CursorLeft - 2;
						// 				Console.Write(country);
						// 				break;
						// 			}
						// 			catch (WebException ex)
						// 			{
						// 				Console.CursorLeft = Console.CursorLeft == 37 ? Console.CursorLeft + 1 : Console.CursorLeft - 2;
						// 				Console.Write(country);
						// 			}
						// 		}
						// 	}
					}

					var json = JObject.Parse(metadataResponse);
					titleData.MetadataJson = json;

					var name = json["title"]["formal_name"].ToString().Replace("\n", " ");
					titleData.Name = name;

					// Console.WriteLine(name);

					// var filename = SanitizeFilename($"{titleId} ({internalId}) - {name}.xml");
					// File.WriteAllText("metadata\\" + filename, metadataXml.ToString());
				}
				catch (WebException ex)
				{
					var response = (HttpWebResponse)ex.Response;
					var statusCode = (int)response.StatusCode;

					// Console.WriteLine($"{titleId} {statusCode}");
				}
			}

			return titleData;
		}

		public static string[] RetrieveInternalInfo(string titleId, X509Certificate2 certificate)
		{
			// from PlaiCDN
			var internalInfo = new string[] { null, null };

			var url = $"https://ninja.ctr.shop.nintendo.net/ninja/ws/titles/id_pair?title_id[]={titleId}";

			using (var client = new CertificateWebClient(certificate))
			{
				client.Headers.Add("Accept", "application/json");
				var response = client.DownloadString(url);

				// var xml = XDocument.Parse(response);
				var json = JObject.Parse(response);

				if (json["title_id_pairs"]["title_id_pair"] != null)
				{
					var internalId = json["title_id_pairs"]["title_id_pair"].First["ns_uid"].ToString();
					var type = json["title_id_pairs"]["title_id_pair"].First["type"].ToString();

					internalInfo[0] = internalId;
					internalInfo[1] = type;
				}
			}

			return internalInfo;
		}

		private static void RetrieveTitles(string country, X509Certificate2 certificate)
		{
			using (var client = new CertificateWebClient(certificate))
			{
				client.Encoding = Encoding.UTF8;
				client.Headers.Add("Accept", "application/json");

				var stopwatch = new Stopwatch();
				stopwatch.Start();

				try
				{
					const string MetadataUrl = "https://samurai.ctr.shop.nintendo.net/samurai/ws/";

					const int TitlesAtATime = 500;

					var metadataResponse =
						JObject.Parse(client.DownloadString(MetadataUrl + country + $"/contents?limit={TitlesAtATime}"));

					var total = metadataResponse["contents"]["total"].Value<int>();
					metadataResponse["contents"]["length"] = total;

					for (int offset = TitlesAtATime; offset < total; offset += TitlesAtATime)
					{
						client.Headers.Add("Accept", "application/json");

						var response = client.DownloadString(MetadataUrl + country + $"/contents?offset={offset}&limit={TitlesAtATime}");
						var responseAsJObject = JObject.Parse(response);

						var contents = metadataResponse["contents"]["content"].Concat(responseAsJObject["contents"]["content"]);
						metadataResponse["contents"]["content"] = JToken.FromObject(contents);

						Console.Write($"\r{country}: {metadataResponse["contents"]["content"].Count()}/{total}");
					}

					stopwatch.Stop();
					var secondsTaken = (double)stopwatch.ElapsedMilliseconds / 1000;

					Console.Write($"\r{country}: {metadataResponse["contents"]["total"].Value<int>()}/{total}".PadRight(15));
					Console.WriteLine($" (Took {(int)secondsTaken / 60:D2}:{secondsTaken % 60:00.00})");

					var indented = JsonConvert.SerializeObject(metadataResponse, Formatting.Indented);
					File.WriteAllText("titles\\" + country + ".json", indented);
				}
				catch (WebException ex)
				{
					var response = (HttpWebResponse)ex.Response;
					var statusCode = response.StatusCode;

					Console.Write($"\r{country}: {(int)statusCode}");
				}
			}
		}

		public static string SanitizeFilename(string input)
		{
			var forbiddenChars = Path.GetInvalidFileNameChars().ToList();
			forbiddenChars.AddRange(new[] { '\n', '™', '©', '®' });

			var sanitized = new StringBuilder();

			foreach (var letter in input)
			{
				sanitized.Append(forbiddenChars.Contains(letter) ? string.Empty : letter.ToString());
			}

			return sanitized.ToString();
		}
	}
}