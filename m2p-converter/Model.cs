// Model.cs
// Created by Oleg Yaroshevych at 02/26/2002

using System;
using System.Collections.Generic;

namespace Converter
{
	public class Protocol
	{
		private List<Contact> contacts = new List<Contact>();

		public Protocol(string protocolName, string userId, string userName)
		{
			ProtocolName = protocolName;
			UserId = userId;
			UserName = userName;
		}

		public void AddContact(Contact contact)
		{
			contacts.Add(contact);
		}

		public string ProtocolName { get; private set; }
		public string UserId { get; private set; }
		public string UserName { get; private set; }

		public IEnumerable<Contact> Contacts {
			get { return contacts; }
		}
	}

	public class Contact
	{
		private List<Message> messages = new List<Message>();

		public Contact(string id, string name)
		{
			Id = id;
			Name = name;
		}

		public void AddMessages(IEnumerable<Message> messages)
		{
			this.messages.AddRange(messages);
		}

		public string Id { get; private set; }
		public string Name { get; private set; }

		public IEnumerable<Message> Messages {
			get { return messages; }
		}
	}

	public class Message
	{
		public Message(DateTime date, MessageType type, string data, string protocol, bool first, bool sent)
		{
			Date = date;
			Type = type;
			Data = data;
			Protocol = protocol;
			First = first;
			Sent = sent;
		}

		public DateTime Date { get; private set; }
		public MessageType Type { get; private set; }
		public string Data { get; private set; }
		public string Protocol { get; private set; }
		public bool First { get; private set; }
		public bool Sent { get; private set; }
	}

	public enum MessageType
	{
		Text,
		Html
	}
}
