// Structures.cs
// Created by Oleg Yaroshevych at 01:13 09/27/2009

using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Converter.Interop
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DBHeader
	{
		/// <summary>
		/// 'Miranda ICQ DB',0,26
		/// </summary>
		[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
		byte[] signature;
		/// <summary>
		/// as 4 bytes, ie 1.2.3.10=0x0102030a this version is 0x00000700
		/// </summary>
		public UInt32 version;
		/// <summary>
		/// offset of the end of the database - place to write new structures
		/// </summary>
		public UInt32 ofsFileEnd;
		/// <summary>
		/// a counter of the number of bytes that have been wasted so far due to deleting structures and/or re-making them at the end. We should compact when this gets above a threshold
		/// </summary> 
		public UInt32 slackSpace;
		/// <summary>
		/// number of contacts in the chain,excluding the user
		/// </summary> 
		public UInt32 contactCount;
		/// <summary>
		/// offset to first struct DBContact in the chain
		/// </summary>
		public UInt32 ofsFirstContact;
		/// <summary>
		/// offset to struct DBContact representing the user
		/// </summary>
		public UInt32 ofsUser;
		/// <summary>
		/// offset to first struct DBModuleName in the chain
		/// </summary>
		public UInt32 ofsFirstModuleName;

		public string GetSignature()
		{
			for (int i = 0; i < signature.Length; ++i)
				if (signature[i] < 32)
					signature[i] = 32;
			
			return Encoding.Default.GetString(signature);
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DBContact
	{
		public UInt32 signature;
		public UInt32 ofsNext;
		//offset to the next contact in the chain. zero if this is the 'user' contact or the last contact in the chain
		public UInt32 ofsFirstSettings;
		// offset to the first DBContactSettings in the chain for this contact.
		public UInt32 eventCount;
		// number of events in the chain for this contact
		public UInt32 ofsFirstEvent, ofsLastEvent;
		// offsets to the first and last DBEvent in the chain for this contact
		UInt32 ofsFirstUnreadEvent;
		// offset to the first (chronological) unread event in the chain, 0 if all are read
		UInt32 timestampFirstUnread;
		// timestamp of the event at ofsFirstUnreadEvent
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DBContactSettings
	{
		public UInt32 signature;
		public UInt32 ofsNext;
		// offset to the next contactsettings in the chain
		public UInt32 ofsModuleName;
		// offset to the DBModuleName of the owner of these settings
		public UInt32 cbBlob;
		// size of the blob in bytes. May be larger than the actual size for reducing the number of moves required using granularity in resizing
		public byte blob;
		// the blob. a back-to-back sequence of DBSetting structs, the last has cbName=0
	}

	enum db_setting_type
	{
		DBVT_DELETED = 0,
		// this setting just got deleted, no other values are valid
		DBVT_BYTE = 1,
		// bVal and cVal are valid
		DBVT_WORD = 2,
		// wVal and sVal are valid
		DBVT_DWORD = 4,
		// dVal and lVal are valid
		DBVT_ASCIIZ = 255,
		// pszVal is valid
		DBVT_BLOB = 254,
		// cpbVal and pbVal are valid
		DBVT_UTF8 = 253,
		// pszVal is valid
		DBVT_WCHAR = 252
		// pszVal is valid
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DBModuleName
	{
		public UInt32 signature;
		public UInt32 ofsNext;
		// offset to the next module name in the chain
		public byte cbName;
		// number of characters in this module name
		byte name;
		// name, no nul terminator
	}

	[Flags]
	enum DBEventFlags
	{
		/// <summary>
		/// this is the first event in the chain; internal only: *do not* use this flag
		/// </summary>
		DBEF_FIRST = 1,
		/// <summary>
		/// this event was sent by the user. If not set this event was received.
		/// </summary>
		DBEF_SENT = 2,
		/// <summary>
		/// event has been read by the user. It does not need to be processed any more except for history.
		/// </summary>
		DBEF_READ = 4,
		/// <summary>
		/// event contains the right-to-left aligned text
		/// </summary>
		DBEF_RTL = 8,
		/// <summary>
		/// event contains a text in utf-8
		/// </summary>
		DBEF_UTF = 16
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct DBEvent
	{
		public UInt32 signature;
		public UInt32 ofsPrev, ofsNext;
		// offset to the previous and next events in the chain. Chain is sorted chronologically
		public UInt32 ofsModuleName;
		// offset to a DBModuleName struct of the name of the owner of this event
		public UInt32 timestamp;
		// seconds since 00:00:00 01/01/1970
		public UInt32 flags;
		// see m_database.h, db/event/add
		public UInt16 eventType;
		// module-defined event type
		public UInt32 cbBlob;
		// number of bytes in the blob
		byte blob;
		// the blob. module-defined formatting
	}

	static class DbValidator
	{
		public static void Validate(DBContact value)
		{
			/*0x43DECADEu*/			Assert(value.signature, 1138674398);
		}

		public static void Validate(DBModuleName value)
		{
			Assert(value.signature, 0x4ddecadeu);
		}

		public static void Validate(DBContactSettings value)
		{
			Assert(value.signature, 0x53decadeu);
		}

		static void Assert(UInt32 value, UInt32 check)
		{
			if (value != check)
				throw new InvalidOperationException("Signature do not match.");
		}
	}
}
