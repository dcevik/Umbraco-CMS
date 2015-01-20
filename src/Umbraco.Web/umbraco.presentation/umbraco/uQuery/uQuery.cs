﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.XPath;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Xml.XPath;
using Umbraco.Web;
using umbraco;
using umbraco.BusinessLogic;
using umbraco.DataLayer;

namespace umbraco
{
	/// <summary>
	/// uQuery - static helper methods
	/// </summary>
	public static partial class uQuery
	{
		/// <summary>
		/// Gets the SqlHelper used by Umbraco
		/// </summary>
		public static ISqlHelper SqlHelper
		{
			get
			{
				return Application.SqlHelper;
			}
		}

		/// <summary>
		/// Gets Xml
		/// </summary>
		/// <param name="umbracoObjectType">an UmbracoObjectType value</param>
		[Obsolete("Obsolete. Use IPublishedCache.CreateNavigator.", false)]
		public static XmlDocument GetPublishedXml(UmbracoObjectType umbracoObjectType)
		{
            // this is here for backward compat only

		    XPathNavigator nav;
            switch (umbracoObjectType)
            {
                case UmbracoObjectType.Media:
                    nav = new RenamedRootNavigator(UmbracoContext.Current.PublishedCaches.MediaCache.CreateNavigator(), "Media");
                    break;

                case UmbracoObjectType.Member:
                    nav = new RenamedRootNavigator(UmbracoContext.Current.PublishedCaches.MemberCache.CreateNavigator(), "Members");
                    break;

                case UmbracoObjectType.Document:
                    nav = new RenamedRootNavigator(UmbracoContext.Current.PublishedCaches.ContentCache.CreateNavigator(false), "Nodes");
                    break;

                default:
                    throw new NotSupportedException("Object type is not supported.");
            }

            // ouch
		    var doc = new XmlDocument();
            doc.LoadXml(nav.OuterXml);
		    return doc;
		}

		/// <summary>
		/// Checks the Umbraco XML Schema version in use
		/// </summary>
		/// <returns>true if using the old XML schema, else false if using the new XML schema</returns>
		public static bool IsLegacyXmlSchema()
		{
			var isLegacyXmlSchema = false;

			try
			{
				isLegacyXmlSchema = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema;
			}
			catch (MissingMethodException)
			{
				// Method doesn't exist so must be using the legacy schema
				isLegacyXmlSchema = true;
			}

			return isLegacyXmlSchema;
		}

		/// <summary>
		/// build a string array from a csv
		/// </summary>
		/// <param name="csv">string of comma seperated values</param>
		/// <returns>An array of node ids as string.</returns>
		public static string[] GetCsvIds(string csv)
		{
			string[] ids = null;

			if (!string.IsNullOrEmpty(csv))
			{
				ids = csv.Split(',').Select(s => s.Trim()).ToArray();
			}

			return ids;
		}

		/// <summary>
		/// Gets Ids from known XML fragments (as saved by the MNTP / XPath CheckBoxList)
		/// </summary>
		/// <param name="xml">The Xml</param>
		/// <returns>An array of node ids as integer.</returns>
		public static int[] GetXmlIds(string xml)
		{
			var ids = new List<int>();

			if (!string.IsNullOrEmpty(xml))
			{
				using (var xmlReader = XmlReader.Create(new StringReader(xml)))
				{
					try
					{
						xmlReader.Read();

						// Check name of first element
						switch (xmlReader.Name)
						{
							case "MultiNodePicker":
							case "XPathCheckBoxList":
							case "CheckBoxTree":

								// Position on first <nodeId>
								xmlReader.ReadStartElement();

								while (!xmlReader.EOF)
								{
									if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "nodeId")
									{
										int id;
										if (int.TryParse(xmlReader.ReadElementContentAsString(), out id))
										{
											ids.Add(id);
										}
									}
									else
									{
										// Step the reader on
										xmlReader.Read();
									}
								}

								break;
						}
					}
					catch
					{
						// Failed to read as Xml
					}
				}
			}

			return ids.ToArray();
		}

		/// <summary>
		/// Gets an Id value from the QueryString
		/// </summary>
		/// <returns>an id as a string or string.empty</returns>
		public static string GetIdFromQueryString()
		{
			var queryStringId = string.Empty;

			if (!string.IsNullOrEmpty(HttpContext.Current.Request.QueryString["id"]))
			{
				queryStringId = HttpContext.Current.Request.QueryString["id"];
			}
			else if (HttpContext.Current.Request.CurrentExecutionFilePathExtension == ".asmx"
					&& HttpContext.Current.Request.UrlReferrer != null
					&& !string.IsNullOrEmpty(HttpContext.Current.Request.UrlReferrer.Query))
			{
				// Special case for MNTP CustomTreeService.asmx
				queryStringId = HttpUtility.ParseQueryString(HttpContext.Current.Request.UrlReferrer.Query)["id"];
			}

			return queryStringId;
		}

		/// <summary>
		/// Converts a string array into an integer array.
		/// </summary>
		/// <param name="items">The string array.</param>
		/// <returns>Returns an integer array.</returns>
		public static int[] ConvertToIntArray(string[] items)
		{
			if (items == null)
				return new int[] { };

			int n;
			return items.Select(s => int.TryParse(s, out n) ? n : 0).ToArray();
		}

		/// <summary>
		/// Generates an XML document.
		/// </summary>
		/// <param name="hierarchy">The hierarchy.</param>
		/// <param name="nodeIndex">Index of the node.</param>
		/// <param name="parentId">The parent id.</param>
		/// <param name="parentNode">The parent node.</param>
		private static void GenerateXmlDocument(IDictionary<int, List<int>> hierarchy, IDictionary<int, XmlNode> nodeIndex, int parentId, XmlNode parentNode)
		{
			List<int> children;

			if (hierarchy.TryGetValue(parentId, out children))
			{
				var childContainer = uQuery.IsLegacyXmlSchema() || string.IsNullOrEmpty(UmbracoSettings.TEMP_FRIENDLY_XML_CHILD_CONTAINER_NODENAME) ? parentNode : parentNode.SelectSingleNode(UmbracoSettings.TEMP_FRIENDLY_XML_CHILD_CONTAINER_NODENAME);

				if (!uQuery.IsLegacyXmlSchema() && !string.IsNullOrEmpty(UmbracoSettings.TEMP_FRIENDLY_XML_CHILD_CONTAINER_NODENAME))
				{
					if (childContainer == null)
					{
						childContainer = xmlHelper.addTextNode(parentNode.OwnerDocument, UmbracoSettings.TEMP_FRIENDLY_XML_CHILD_CONTAINER_NODENAME, string.Empty);
						parentNode.AppendChild(childContainer);
					}
				}

				foreach (int childId in children)
				{
					var childNode = nodeIndex[childId];

					if (uQuery.IsLegacyXmlSchema() || string.IsNullOrEmpty(UmbracoSettings.TEMP_FRIENDLY_XML_CHILD_CONTAINER_NODENAME))
					{
						parentNode.AppendChild(childNode);
					}
					else
					{
						childContainer.AppendChild(childNode);
					}

					// Recursively build the content tree under the current child
					GenerateXmlDocument(hierarchy, nodeIndex, childId, childNode);
				}
			}
		}
	}
}