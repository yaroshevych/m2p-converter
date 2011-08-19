// Database.cs
// Created by Oleg Yaroshevych at 01:19 09/27/2009

using System;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Converter.Interop;

namespace Converter
{
	public class Database
	{
		readonly string path;
		readonly Encoding encoding;

		public Database(string path, Encoding encoding)
		{
			this.path = path;
			this.encoding = encoding;
		}

		public List<Protocol> Read()
		{
			List<Protocol> protocols = new List<Protocol>();
			
			using (FileStream file = File.Open(path, FileMode.Open)) {
				DBHeader header = ReadStructure<DBHeader>(file, 0);
				Dictionary<uint, ModuleName> modules = ReadModuleNames(file, header.ofsFirstModuleName);
				
				uint offset = header.ofsFirstContact;
#if DEBUG
				Console.WriteLine("Got " + header.contactCount + " contacts");
#endif
				
				var dbContact = ReadContact(file, modules, ref header.ofsUser);
				protocols.Add(new Protocol("icq", GetContactSettingString(dbContact, "icq", "UIN"), GetContactSettingString(dbContact, "icq", "Nick")));
				
				for (int i = 0; i < header.contactCount; ++i) {
					dbContact = ReadContact(file, modules, ref offset);
					var messages = ReadMessages(dbContact.contact, file, modules, encoding);
					
					if (messages.Count() > 0) {
						var contact = new Contact(GetContactSettingString(dbContact, "icq", "UIN"), GetContactSettingString(dbContact, "icq", "Nick"));
						contact.AddMessages(messages);
						protocols[0].AddContact(contact);
					}
					
					if (offset == 0)
						break;
				}
			}
			
			return protocols;
		}

		private static string GetContactSettingString(DbContactContext contact, string moduleName, string settingName)
		{
			var moduleSettings = contact.Settings.FirstOrDefault(s => string.Equals(s.Name, moduleName, StringComparison.OrdinalIgnoreCase));
			return Convert.ToString(moduleSettings.Settings.First(s => string.Equals(s.Name, settingName, StringComparison.OrdinalIgnoreCase)).Value);
		}

		private static Dictionary<uint, ModuleName> ReadModuleNames(FileStream file, uint offset)
		{
			Dictionary<uint, ModuleName> modules = new Dictionary<uint, ModuleName>();
			
			while (offset != 0) {
				DBModuleName moduleName = ReadStructure<DBModuleName>(file, offset);
				DbValidator.Validate(moduleName);
				file.Seek(offset + 9, SeekOrigin.Begin);
				byte[] data = new byte[moduleName.cbName];
				file.Read(data, 0, (int)moduleName.cbName);
				
				int len = data.Length;
				
				for (int i = 0; i < data.Length; ++i)
					if (data[i] == 0) {
						len = i;
						break;
					}
				
				string name = System.Text.Encoding.ASCII.GetString(data, 0, len);
				
				if (modules.ContainsKey(offset))
					break;
				
				modules.Add(offset, new ModuleName(name, offset));
				offset = moduleName.ofsNext;
			}
			
			return modules;
		}

		private static DbContactContext ReadContact(FileStream file, Dictionary<uint, ModuleName> modules, ref uint offset)
		{
			DBContact contact = ReadStructure<DBContact>(file, offset);
			DbValidator.Validate(contact);
			offset = contact.ofsNext;
			
			// read contact info
			
			uint settingOffset = contact.ofsFirstSettings;
			List<DbContactSettings> settings = new List<DbContactSettings>();
			
			while (settingOffset != 0) {
				DbContactSettings setting = ReadContactSettings(file, modules, ref settingOffset);
				settings.Add(setting);
			}
			
			return new DbContactContext(contact, settings.ToArray());
		}

		private static DbContactSettings ReadContactSettings(FileStream file, Dictionary<uint, ModuleName> modules, ref uint offset)
		{
			DBContactSettings setting = ReadStructure<DBContactSettings>(file, offset);
			DbValidator.Validate(setting);
			
			string moduleName = modules[setting.ofsModuleName].Name;
			
			byte[] buffer = new byte[setting.cbBlob];
			file.Seek(offset + 16, SeekOrigin.Begin);
			file.Read(buffer, 0, buffer.Length);
			int pos = 0;
			offset = setting.ofsNext;
			List<DbSetting> settings = new List<DbSetting>();
			
			while (buffer[pos] != 0) {
				int len = buffer[pos];
				string name = Encoding.ASCII.GetString(buffer, pos + 1, len);
				pos += len + 1;
				db_setting_type type = (db_setting_type)buffer[pos];
				
				switch (type) {
				case db_setting_type.DBVT_DELETED:
					break;
				case db_setting_type.DBVT_BYTE:
					settings.Add(new DbSetting(name, type, buffer[pos + 1]));
					pos += 2;
					break;
				case db_setting_type.DBVT_WORD:
					settings.Add(new DbSetting(name, type, BitConverter.ToInt16(buffer, pos + 1)));
					pos += 3;
					break;
				case db_setting_type.DBVT_DWORD:
					settings.Add(new DbSetting(name, type, BitConverter.ToInt32(buffer, pos + 1)));
					pos += 5;
					break;
				case db_setting_type.DBVT_BLOB:
					len = BitConverter.ToUInt16(buffer, pos + 1);
					settings.Add(new DbSetting(name, type, "BLOB:" + len));
					pos += len + 3;
					break;
				case db_setting_type.DBVT_ASCIIZ:
					len = BitConverter.ToUInt16(buffer, pos + 1);
					settings.Add(new DbSetting(name, type, Encoding.ASCII.GetString(buffer, pos + 3, len)));
					pos += len + 3;
					break;
				case db_setting_type.DBVT_UTF8:
					len = BitConverter.ToUInt16(buffer, pos + 1);
					settings.Add(new DbSetting(name, type, Encoding.UTF8.GetString(buffer, pos + 3, len)));
					pos += len + 3;
					break;
				case db_setting_type.DBVT_WCHAR:
					len = BitConverter.ToUInt16(buffer, pos + 1);
					settings.Add(new DbSetting(name, type, Encoding.Unicode.GetString(buffer, pos + 3, len)));
					pos += len + 3;
					break;
				default:
					throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "'{0}' is not supported.", buffer[pos]));
				}
			}
			
			return new DbContactSettings(moduleName, settings.ToArray());
		}

		private static T ReadStructure<T>(FileStream file, uint offset) where T : struct
		{
			Type type = typeof(T);
			
			#if DEBUG
			// Console.WriteLine("Reading " + type.Name + " at " + offset);
			#endif
			
			byte[] data = new byte[Marshal.SizeOf(type)];
			file.Seek(offset, SeekOrigin.Begin);
			file.Read(data, 0, data.Length);
			int size = Marshal.SizeOf(type);
			
			if (size > data.Length)
				throw new InvalidOperationException("Array size does not match structure size.");
			
			IntPtr buffer = Marshal.AllocHGlobal(size);
			
			try {
				Marshal.Copy(data, 0, buffer, size);
				return (T)Marshal.PtrToStructure(buffer, type);
			} finally {
				Marshal.FreeHGlobal(buffer);
			}
		}

		private static List<Message> ReadMessages(DBContact contact, FileStream file, Dictionary<uint, ModuleName> modules, Encoding encoding)
		{
			List<Message> messages = new List<Message>((int)contact.eventCount);
			UInt32 offset = contact.ofsFirstEvent;
			
			while (offset != 0) {
				DBEvent eventData = Database.ReadStructure<DBEvent>(file, offset);
				
				file.Seek(offset + 30, SeekOrigin.Begin);
				byte[] data = new byte[eventData.cbBlob];
				file.Read(data, 0, (int)eventData.cbBlob);
				
				int len = data.Length;
				
				for (int i = 0; i < data.Length; ++i)
					if (data[i] == 0) {
						len = i;
						break;
					}
				
				string messageData = ((DBEventFlags)eventData.flags & DBEventFlags.DBEF_UTF) == DBEventFlags.DBEF_UTF ? Encoding.UTF8.GetString(data, 0, len) : encoding.GetString(data, 0, len);
				
				//string test = Encoding.UTF8.GetString(data, 0, len);
				
				//if (test.Length < messageData.Length)
				//	messageData = test;
				
				bool firstMessage = ((DBEventFlags)eventData.flags & DBEventFlags.DBEF_FIRST) == DBEventFlags.DBEF_FIRST;
				bool sentMessage = ((DBEventFlags)eventData.flags & DBEventFlags.DBEF_SENT) == DBEventFlags.DBEF_SENT;
				messages.Add(new Message(ConvertFromUnixTimestamp(eventData.timestamp), MessageType.Text, messageData, modules[eventData.ofsModuleName].Name, firstMessage, sentMessage));
				offset = eventData.ofsNext;
			}
			
			return messages;
		}

		private static DateTime ConvertFromUnixTimestamp(double timestamp)
		{
			DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
			return origin.AddSeconds(timestamp);
		}

		private class DbContactContext
		{
			public readonly DbContactSettings[] Settings;
			public readonly DBContact contact;

			public DbContactContext(DBContact dbContact, DbContactSettings[] settings)
			{
				Settings = settings;
				contact = dbContact;
			}

			public readonly int MessageCount;
		}

		private class ModuleName
		{
			public readonly uint Offset;
			public readonly string Name;

			public ModuleName(string name, uint offset)
			{
				Name = name;
				Offset = offset;
			}
		}

		/// <summary>
		/// Represents set of user's settings belonging to particulat module.
		/// </summary>
		private class DbContactSettings
		{
			public readonly string Name;

			// TODO: replace settings with Dictionary<string, string> - name-value
			public readonly DbSetting[] Settings;

			public DbContactSettings(string name, DbSetting[] settings)
			{
				Name = name;
				Settings = settings;
			}
		}

		private class DbSetting
		{
			public readonly string Name;
			public readonly db_setting_type Type;
			public readonly object Value;

			public DbSetting(string name, db_setting_type type, object value)
			{
				Name = name;
				Type = type;
				Value = value;
			}
		}
	}
}
