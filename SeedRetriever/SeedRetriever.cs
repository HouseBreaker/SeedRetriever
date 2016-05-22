﻿namespace SeedRetriever
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Security;
	using System.Security.Cryptography;
	using System.Security.Cryptography.X509Certificates;
	using System.Text;

	using Shameless.Utils;

	public static class SeedRetriever
	{
		public static void Main(string[] args)
		{
			ServicePointManager.ServerCertificateValidationCallback = (sender, x509Certificate, chain, errors) => true;

			var titles = File.ReadAllLines("titles.txt");

			const string lastIndexPath = "lastIndex.txt";
			var lastIndex = int.Parse(File.ReadAllText(lastIndexPath));

			using (var client = new WebClient())
			{
				for (int i = lastIndex; i < titles.Length; i++)
				{
					var titleId = titles[i];
					const string country = "US";

					// string internalId;
					try
					{
						const string CdnUrl = "https://kagiya-ctr.cdn.nintendo.net/title/0x{0}/ext_key?country={1}";
						var seedAsBytes = client.DownloadData(string.Format(CdnUrl, titleId, country));

						var seed = BitConversion.BytesToHex(seedAsBytes);
						var titleSeedPair = $"{titleId}: {seed}{Environment.NewLine}";

						Console.Write(titleSeedPair);
						File.AppendAllText("seeds.txt", titleSeedPair);

						// var response = client.DownloadString($"https://ninja.ctr.shop.nintendo.net/ninja/ws/titles/id_pair?title_id[]={titleId}");

						// const string idTag = "<ns_uid>";
						// var idBegin = response.IndexOf(idTag) + idTag.Length;
						// internalId = response.Substring(idBegin, 14);
						// Console.WriteLine($"Internal ID: {internalId}");
					}
					catch (WebException ex)
					{
						var response = (HttpWebResponse)ex.Response;
						var statusCode = (int)response.StatusCode;

						Console.WriteLine("{0}: {1}", titleId, statusCode);
						File.AppendAllText("nonseeds.txt", $"{titleId}: {statusCode}{Environment.NewLine}");
					}

					File.WriteAllText(lastIndexPath, i.ToString());
				}
			}
		}
	}
}