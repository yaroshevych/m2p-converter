// OutputFileHelper.cs
// Created by Oleg Yaroshevych at 02/26/2002

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace Converter
{
	public class OutputFileHelper
	{
		public static void WriteToFiles(string path, IEnumerable<Protocol> protocols)
		{
			TimeZoneInfo tz = TimeZoneInfo.Local;
			
			foreach (var protocol in protocols) {
				string protocolDirectory = Path.Combine(path, protocol.ProtocolName);
				protocolDirectory = Path.Combine(protocolDirectory, protocol.UserId);
				Directory.CreateDirectory(protocolDirectory);
				
				foreach (var contact in protocol.Contacts) {
					string contactDirectory = Path.Combine(protocolDirectory, contact.Id);
					Directory.CreateDirectory(contactDirectory);
					DateTime lastMessage = DateTime.MinValue;
					StreamWriter file = null;
					
					try {
						foreach (var message in contact.Messages) {
							if (message.Date > lastMessage.AddMinutes(30) || file == null) {
								if (file != null)
									file.Close();
								
								string fileName = message.Date.ToString("yyyy-MM-dd.HHmmss") + tz.BaseUtcOffset.Hours.ToString() + tz.BaseUtcOffset.Minutes.ToString() + tz.DaylightName + ".txt";
								fileName = Path.Combine(contactDirectory, fileName);
								
								file = new StreamWriter(fileName, false, Encoding.UTF8);
								file.WriteLine(string.Format("Conversation with {0} at {1} on {2} (icq)", contact.Id, message.Date.ToString(), protocol.UserId));
								lastMessage = message.Date;
							}
							
							file.WriteLine(string.Format("({0}) {1}: {2}", message.Date.ToLongTimeString(), message.Sent ? protocol.UserName : contact.Name, message.Data));
						}
					} finally {
						if (file != null)
							file.Close();
					}
				}
			}
		}
	}
}
