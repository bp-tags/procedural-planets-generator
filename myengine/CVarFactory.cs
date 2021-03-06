﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neitri;
using System.Globalization;

namespace MyEngine
{
	public class CVarFactory
	{

		Dictionary<string, CVar> nameToCVar = new Dictionary<string, CVar>();

		public IReadOnlyDictionary<string, CVar> NameToCvar => nameToCVar;

		bool doSaveNewCvars = false;
		public readonly ILog Log;


		struct LineHolder
		{
			public string dataPart;
			public string commentPart;
			public CVar associatedCvar;
		}
		List<LineHolder> saveData = new List<LineHolder>();


		FileExisting file;
		DateTime fileLastSaved = DateTime.MinValue;
		public CVarFactory(FileExisting file, ILog log)
		{

			this.Log = log;
			this.file = file;
			TryReadConfig();

			//file.OnFileChanged(() =>
			//{
			//	// make sure our save does not trigger file changed event and reload
			//	if (fileLastSaved.IsOver(seconds: 10).InPastComparedTo(DateTime.Now))
			//	{
			//		TryReadConfig();
			//	}
			//});

			GetCVar("debug / save cvars to file", false).OnChanged(cvar =>
			{
				if (cvar.EatBoolIfTrue())
				{
					GetCVar("debug / show debug form").Bool = false;
					SaveData();
				}
			});

		}

		void TryReadConfig()
		{

			doSaveNewCvars = false;

			var configFile = file.OpenReadWrite();
			if (!configFile.CanRead)
			{
				Log.Fatal("can not read config file");
				return;
			}

			configFile.Position = 0;
			var reader = new StreamReader(configFile, Encoding.UTF8);
			var allText = reader.ReadToEnd();
			reader.Close();

			var textLines = allText.Split(new char[] { '\r' });
			saveData.Clear();

			int lineNumber = 0;
			foreach (var _line in textLines)
			{
				lineNumber++;

				CVar cvar = null;
				string dataPart = _line.Trim();
				string commentPart = "";

				var commentIndex = dataPart.IndexOfAny(new char[] { '#' }); // ab#c // # is comment
				if (commentIndex != -1) // 2
				{
					commentPart = dataPart.Trim().Substring(commentIndex); // #c
					dataPart = dataPart.Trim().Substring(0, commentIndex); // ab
				}

				if (dataPart.IsNullOrEmptyOrWhiteSpace() == false)
				{
					var dataParts = dataPart.Split(new char[] { '=' });
					if (dataParts.Length < 2)
					{
						Log.Error("found badly formatted data on line " + lineNumber + " '" + dataPart + "'");
						continue;
					}

					var name = dataParts[0].Trim();
					cvar = GetCVar(name);

					bool typedBoolValue;
					float typedFloatValue;
					OpenTK.Input.Key keyTyped;

					var value = dataParts[1].Trim();
					if (value == "not set")
					{
						cvar.ValueType = CvarValueType.NotSet;
					}
					else if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out typedFloatValue))
					{
						cvar.Number = typedFloatValue;
					}
					else if (bool.TryParse(value, out typedBoolValue))
					{
						cvar.Bool = typedBoolValue;
					}

					if (dataParts.Length > 2)
					{
						var toggleKey = dataParts[2].Trim();
						if (Enum.TryParse<OpenTK.Input.Key>(toggleKey, true, out keyTyped))
							cvar.ToogledByKey(keyTyped);
						else
							Log.Warn("invalid toggle key for cvar: " + toggleKey);
					}

					Log.Info("loaded cvar: '" + ToSaveString(cvar) + "' from line: '" + dataPart + "'");
				}

				saveData.Add(new LineHolder() { associatedCvar = cvar, commentPart = commentPart, dataPart = dataPart });
			}

			doSaveNewCvars = true;
		}
		private string ToSaveString(CVar cvar)
		{
			var s = cvar.Name + " = ";

			if (cvar.ValueType == CvarValueType.NotSet) s += "not set";
			else if (cvar.ValueType == CvarValueType.Bool) s += cvar.Bool.ToString(CultureInfo.InvariantCulture).ToLower();
			else if (cvar.ValueType == CvarValueType.Number) s += cvar.Number.ToString(CultureInfo.InvariantCulture).ToLower();

			if (cvar.ToogleKey != OpenTK.Input.Key.Unknown) s += " = " + cvar.ToogleKey.ToString().ToLower();

			return s;
		}


		private void SaveData()
		{
			fileLastSaved = DateTime.Now;

			var configFile = file.OpenReadWrite();
			if (!configFile.CanSeek)
			{
				Log.Fatal("can not seek config file");
				return;
			}
			if (!configFile.CanWrite)
			{
				Log.Fatal("can not write to config file");
				return;
			}

			configFile.Position = 0;
			var w = new StreamWriter(configFile, Encoding.UTF8);

			var isFirst = true;
			foreach (var line in saveData)
			{
				if (isFirst == false) w.WriteLine();

				var l = "";
				if (line.associatedCvar != null) l += ToSaveString(line.associatedCvar) + " ";
				l += line.commentPart;
				w.Write(l);

				isFirst = false;
			}
			w.Flush();

			// clear the rest of the file, we cant actually decrease its size it seems
			if (w.BaseStream.Length > w.BaseStream.Position)
			{
				var c = w.BaseStream.Length - w.BaseStream.Position;
				while (c-- > 0)
					w.Write(" ");
				w.Flush();
			}
			w.Close();
		}

		private void SaveNewCvar(CVar cvar)
		{
			saveData.Add(new LineHolder()
			{
				dataPart = ToSaveString(cvar),
				associatedCvar = cvar,
				commentPart = "",
			});
		}

		public CVar GetCVar(string name)
		{
			name = name.Trim();
			CVar cvar;
			if (!nameToCVar.TryGetValue(name, out cvar))
			{
				cvar = new CVar(name, this);
				nameToCVar[name] = cvar;
				if (doSaveNewCvars) SaveNewCvar(cvar);
			}
			return cvar;
		}
		public CVar GetCVar(string name, bool defaultValue = false)
		{
			name = name.Trim();
			CVar cvar;
			if (!nameToCVar.TryGetValue(name, out cvar))
			{
				cvar = new CVar(name, this);
				cvar.Bool = defaultValue;
				nameToCVar[name] = cvar;
				if (doSaveNewCvars) SaveNewCvar(cvar);
			}
			return cvar;
		}
		public CVar GetCVar(string name, float defaultValue = 0)
		{
			name = name.Trim();
			CVar cvar;
			if (!nameToCVar.TryGetValue(name, out cvar))
			{
				cvar = new CVar(name, this);
				cvar.Number = defaultValue;
				nameToCVar[name] = cvar;
				if (doSaveNewCvars) SaveNewCvar(cvar);
			}
			return cvar;
		}



	}
}
