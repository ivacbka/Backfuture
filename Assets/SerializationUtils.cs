using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public static class SerializationUtils<T> where T : class
{

	public static void SerializeXml(T toSerialize, string fileName, Type[] extraTypes)
	{
		XmlSerializer serializer = new XmlSerializer(typeof(T), extraTypes);

		var writer = new StreamWriter(new FileStream(fileName, FileMode.Create), new System.Text.UTF8Encoding(false));
		writer.NewLine = "\r\n";
		serializer.Serialize(writer, toSerialize);
		writer.Close();
	}
        
	public static string SerializeXml(T toSerialize, Type[] extraTypes)
	{
		var xmlSerializer = new XmlSerializer(toSerialize.GetType(), extraTypes);
		using(var textWriter = new StringWriter())
		{
			xmlSerializer.Serialize(textWriter, toSerialize);
			return textWriter.ToString();
		}
	}

	public static T DeserializeXml(Stream input, Type[] extraTypes)
	{
		T result = null;

		XmlSerializer serializer = new XmlSerializer(typeof(T), extraTypes);
		result = (T)serializer.Deserialize(new System.IO.StreamReader(input, new System.Text.UTF8Encoding(false)));

		return result;
	}
        
	public static T DeserializeXmlString(string contents, Type[] extraTypes)
	{
		T result = null;

		XmlSerializer serializer = new XmlSerializer(typeof(T), extraTypes);
		result = (T)serializer.Deserialize(new StringReader(contents));

		return result;
	}

	public static T DeserializeXml(string fileName, Type[] extraTypes)
	{
		T result = null;

		if (File.Exists(fileName))
		{
			XmlSerializer serializer = new XmlSerializer(typeof(T), extraTypes);
			using (var reader = new StreamReader(new FileStream(fileName, FileMode.Open), System.Text.Encoding.UTF8))
			{
				result = (T)serializer.Deserialize(reader);
			}
		}

		return result;
	}
}
