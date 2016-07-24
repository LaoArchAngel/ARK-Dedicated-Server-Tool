﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ARK_Server_Manager.Lib.Utils;

namespace ARK_Server_Manager.Lib
{
    public enum IniFiles
    {
        GameUserSettings,
        Game
    }

    public enum IniFileSections
    {
        ServerSettings,
        SessionSettings,
        GameSession,
        GameMode,
        MessageOfTheDay,
        MultiHome
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple=true)]
    public class IniFileEntryAttribute : Attribute
    {
        /// <summary>
        /// Attribute for the IniFile serializer
        /// </summary>
        /// <param name="File">The file into which the setting should be serialized.</param>
        /// <param name="Section">The section in the ini file.</param>
        /// <param name="Key">The key within the section.  Defaults to the same name as the attributed field.</param>
        public IniFileEntryAttribute(IniFiles file, IniFileSections section, string key = "")
        {
            this.File = file;
            this.Section = section;
            this.Key = key;
        }

        public IniFiles File;

        /// <summary>
        /// The section of the ini file.
        /// </summary>
        public IniFileSections Section;

        /// <summary>
        /// The key within the section.
        /// </summary>
        public string Key;

        /// <summary>
        /// If true, the value of booleans will be inverted when read or written.
        /// </summary>
        public bool InvertBoolean;

        /// <summary>
        /// If true, will also write a true boolean value when the underlying field is non-default (or empty for strings), otherwise a false value will be written.
        /// </summary>
        public bool WriteBoolValueIfNonEmpty;

        /// <summary>
        /// Clear the section before writing this value.
        /// </summary>
        public bool ClearSection;

        /// <summary>
        /// If true, the value will always be written with quotes
        /// </summary>
        public bool QuotedString;

        /// <summary>
        /// Only write the attributed value if the named field is true.
        /// </summary>
        public string ConditionedOn;

        /// <summary>
        /// If true, the value will be treated as a multiline value.
        /// </summary>
        public bool Multiline;

        /// <summary>
        /// Clears the value when the named field is off, otherwise if on will skip the update. 
        /// NOTE: Use this for config fields that are updated by the server, while it is ruuning.
        /// </summary>
        public string ClearWhenOff;
    }

    /// <summary>
    /// Class for reading/writing INI files
    /// </summary>
    /// <remarks>
    /// From http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C
    /// </remarks>
    class SystemIniFile
    {
        private readonly Dictionary<IniFileSections, string> SectionNames = new Dictionary<IniFileSections, string>()
        {
            { IniFileSections.GameMode, "/script/shootergame.shootergamemode" },
            { IniFileSections.GameSession, "/Script/Engine.GameSession"},
            { IniFileSections.MessageOfTheDay, "MessageOfTheDay" },
            { IniFileSections.MultiHome, "MultiHome" },
            { IniFileSections.ServerSettings, "ServerSettings" },
            { IniFileSections.SessionSettings, "SessionSettings" },
        };

        private readonly Dictionary<IniFiles, string> FileNames = new Dictionary<IniFiles, string>()
        {
            { IniFiles.GameUserSettings, "GameUserSettings.ini" },
            { IniFiles.Game, "Game.ini" }
        };

        public string basePath;

        public SystemIniFile(string INIPath)
        {
            basePath = INIPath;
        }

        public void Deserialize(object obj)
        {
            Dictionary<string, IniFile> iniFiles = new Dictionary<string, IniFile>();

            var fields = obj.GetType().GetProperties().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
            foreach (var field in fields)
            {
                var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);
                foreach (var attribute in attributes)
                {
                    var attr = attribute as IniFileEntryAttribute;
                    var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

                    if (attr.WriteBoolValueIfNonEmpty)
                    {
                        // Don't really need to do anything here, we don't care about this on reading it.
                        // extraBoolValue = Convert.ToBoolean(IniReadValue(SectionNames[attr.Section], attr.Key));
                    }
                    else
                    {
                        var iniValue = ReadValue(iniFiles, attr.File, attr.Section, keyName);
                        var fieldType = field.PropertyType;
                        var collection = field.GetValue(obj) as IIniValuesCollection;

                        if (collection != null)
                        {
                            var section = ReadSection(iniFiles, attr.File, attr.Section);
                            var filteredSection = collection.IsArray ? section.Where(s => s.StartsWith(collection.IniCollectionKey + "[")) :
                                                                       section.Where(s => s.StartsWith(collection.IniCollectionKey + "="));
                            collection.FromIniValues(filteredSection);
                        }
                        else if (fieldType == typeof(string))
                        {
                            var stringValue = iniValue;
                            if (attr.Multiline)
                            {
                                stringValue = stringValue.Replace(@"\n", Environment.NewLine);
                            }
                            field.SetValue(obj, stringValue);
                        }
                        else
                        {
                            // Update the ConditionedOn flag, if this field has one.
                            if (!String.IsNullOrWhiteSpace(attr.ConditionedOn))
                            {
                                var conditionField = obj.GetType().GetProperty(attr.ConditionedOn);
                                if (String.IsNullOrWhiteSpace(iniValue))
                                {
                                    conditionField.SetValue(obj, false);
                                }
                                else
                                {
                                    conditionField.SetValue(obj, true);
                                }
                            }

                            if (String.IsNullOrWhiteSpace(iniValue))
                            {
                                // Skip non-string values which are not found
                                continue;
                            }

                            var valueSet = StringUtils.SetPropertyValue(iniValue, obj, field, attr);
                            if (!valueSet)
                                throw new ArgumentException(String.Format("Unexpected field type {0} for INI key {1} in section {2}.", fieldType.ToString(), keyName, attr.Section));
                        }
                    }
                }
            }
        }

        public void Serialize(object obj)
        {
            Dictionary<string, IniFile> iniFiles = new Dictionary<string, IniFile>();

            var fields = obj.GetType().GetProperties().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
            foreach (var field in fields)
            {
                var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);
                foreach (var attribute in attributes)
                {
                    var attr = attribute as IniFileEntryAttribute;
                    var value = field.GetValue(obj);
                    var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

                    if (attr.ClearSection)
                    {
                        WriteValue(iniFiles, IniFiles.GameUserSettings, attr.Section, null, null);
                    }

                    //
                    // If this is a collection, we need to first remove all of its values from the INI.
                    //
                    IIniValuesCollection collection = value as IIniValuesCollection;
                    if (collection != null)
                    {
                        var section = ReadSection(iniFiles, attr.File, attr.Section);
                        var filteredSection = section
                                                    .Where(s => !s.StartsWith(collection.IniCollectionKey + (collection.IsArray ? "[" : "=")))
                                                    .ToArray();
                        WriteSection(iniFiles, attr.File, attr.Section, filteredSection);
                    }

                    if (!String.IsNullOrEmpty(attr.ConditionedOn))
                    {
                        var conditionField = obj.GetType().GetProperty(attr.ConditionedOn);
                        var conditionValue = conditionField.GetValue(obj);
                        if (conditionValue is bool && (bool)conditionValue == false)
                        {
                            // The condition value was not set to true, so clear this attribute instead of writing it
                            WriteValue(iniFiles, attr.File, attr.Section, keyName, null);
                            continue;
                        }
                    }

                    if (!String.IsNullOrEmpty(attr.ClearWhenOff))
                    {
                        var updateOffField = obj.GetType().GetProperty(attr.ClearWhenOff);
                        var updateOffValue = updateOffField.GetValue(obj);
                        if (updateOffValue is bool && (bool)updateOffValue == false)
                        {
                            // The attributed value was set to false, so clear this attribute instead of writing it
                            WriteValue(iniFiles, attr.File, attr.Section, keyName, null);
                        }
                        continue;
                    }

                    if (attr.WriteBoolValueIfNonEmpty)
                    {
                        if (value == null)
                        {
                            WriteValue(iniFiles, attr.File, attr.Section, keyName, "False");
                        }
                        else
                        {
                            if (value is string)
                            {
                                var strValue = value as string;
                                WriteValue(iniFiles, attr.File, attr.Section, keyName, String.IsNullOrEmpty(strValue) ? "False" : "True");
                            }
                            else
                            {
                                // Not supported
                                throw new NotSupportedException("Unexpected IniFileEntry value type.");
                            }
                        }
                    }
                    else
                    {
                        if (collection != null)
                        {
                            if (collection.IsEnabled)
                            {
                                // Remove all the values in the collection with this key name
                                var section = ReadSection(iniFiles, attr.File, attr.Section);
                                var filteredSection = collection.IsArray ? section.Where(s => !s.StartsWith(keyName + "["))
                                                                         : section.Where(s => !s.StartsWith(keyName + "="));
                                var result = filteredSection.Concat(collection.ToIniValues()).ToArray();
                                WriteSection(iniFiles, attr.File, attr.Section, result);
                            }
                        }
                        else
                        {
                            var strValue = StringUtils.GetPropertyValue(value, field, attr);
                            if (attr.QuotedString && !(strValue.StartsWith("\"") && strValue.EndsWith("\"")))
                            {
                                strValue = "\"" + strValue + "\"";
                            }

                            if (attr.Multiline)
                            {
                                // substitutes the NewLine string with "\n"
                                strValue = strValue.Replace(Environment.NewLine, @"\n");
                            }

                            WriteValue(iniFiles, attr.File, attr.Section, keyName, strValue);
                        }
                    }
                }
            }

            SaveFiles(iniFiles);
        }

        public string[] ReadSection(IniFiles iniFile, IniFileSections section)
        {
            var file = Path.Combine(this.basePath, FileNames[iniFile]);
            return IniFileUtils.ReadSection(file, SectionNames[section]);
        }

        public void WriteSection(IniFiles iniFile, IniFileSections section, string[] values)
        {
            var file = Path.Combine(this.basePath, FileNames[iniFile]);
            var result = IniFileUtils.WriteSection(file, SectionNames[section], values);
        }

        private string[] ReadSection(Dictionary<string, IniFile> iniFiles, IniFiles iniFile, IniFileSections section)
        {
            if (!iniFiles.ContainsKey(FileNames[iniFile]))
            {
                var file = Path.Combine(this.basePath, FileNames[iniFile]);
                iniFiles.Add(FileNames[iniFile], IniFileUtils.ReadFromFile(file));
            }

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                return new string[0];

            return iniFiles[FileNames[iniFile]].GetSection(SectionNames[section])?.KeysToStringArray() ?? new string[0];
        }

        private string ReadValue(Dictionary<string, IniFile> iniFiles, IniFiles iniFile, IniFileSections section, string keyName)
        {
            if (!iniFiles.ContainsKey(FileNames[iniFile]))
            {
                var file = Path.Combine(this.basePath, FileNames[iniFile]);
                iniFiles.Add(FileNames[iniFile], IniFileUtils.ReadFromFile(file));
            }

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                return string.Empty;

            return iniFiles[FileNames[iniFile]].GetKey(SectionNames[section], keyName)?.KeyValue ?? string.Empty;
        }

        private void WriteSection(Dictionary<string, IniFile> iniFiles, IniFiles iniFile, IniFileSections section, string[] values)
        {
            var file = Path.Combine(this.basePath, FileNames[iniFile]);

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                iniFiles.Add(FileNames[iniFile], IniFileUtils.ReadFromFile(file));

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                return;

            iniFiles[FileNames[iniFile]].WriteSection(SectionNames[section], values);
        }

        private void WriteValue(Dictionary<string, IniFile> iniFiles, IniFiles iniFile, IniFileSections section, string keyName, string keyValue)
        {
            var file = Path.Combine(this.basePath, FileNames[iniFile]);

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                iniFiles.Add(FileNames[iniFile], IniFileUtils.ReadFromFile(file));

            if (!iniFiles.ContainsKey(FileNames[iniFile]))
                return;

            iniFiles[FileNames[iniFile]].WriteKey(SectionNames[section], keyName, keyValue);
        }

        private void SaveFiles(Dictionary<string, IniFile> iniFiles)
        {
            //foreach (var iniFile in iniFiles)
            //{
            //    var file = Path.Combine(this.basePath, iniFile.Key);
            //    var result = IniFileUtils.SaveToFile(file, iniFile.Value);
            //}
            Parallel.ForEach(iniFiles, iniFile => {
                var file = Path.Combine(this.basePath, iniFile.Key);
                var result = IniFileUtils.SaveToFile(file, iniFile.Value);
            });
        }

        //public void Deserialize(object obj)
        //{
        //    var fields = obj.GetType().GetProperties().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
        //    foreach (var field in fields)
        //    {
        //        var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);
        //        foreach (var attribute in attributes)
        //        {
        //            var attr = attribute as IniFileEntryAttribute;
        //            var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

        //            if (attr.WriteBoolValueIfNonEmpty)
        //            {
        //                // Don't really need to do anything here, we don't care about this on reading it.
        //                // extraBoolValue = Convert.ToBoolean(IniReadValue(SectionNames[attr.Section], attr.Key));
        //            }
        //            else
        //            {
        //                var iniValue = IniReadValue(attr.File, attr.Section, keyName);
        //                var fieldType = field.PropertyType;
        //                var collection = field.GetValue(obj) as IIniValuesCollection;

        //                if (collection != null)
        //                {
        //                    var section = IniReadSection(attr.File, attr.Section);
        //                    var filteredSection = collection.IsArray ? section.Where(s => s.StartsWith(collection.IniCollectionKey + "[")) :
        //                                                               section.Where(s => s.StartsWith(collection.IniCollectionKey + "="));
        //                    collection.FromIniValues(filteredSection);
        //                }
        //                else if (fieldType == typeof(string))
        //                {
        //                    var stringValue = iniValue;
        //                    if (attr.Multiline)
        //                    {
        //                        stringValue = stringValue.Replace(@"\n", Environment.NewLine);
        //                    }
        //                    field.SetValue(obj, stringValue);
        //                }
        //                else
        //                {
        //                    // Update the ConditionedOn flag, if this field has one.
        //                    if (!String.IsNullOrWhiteSpace(attr.ConditionedOn))
        //                    {
        //                        var conditionField = obj.GetType().GetProperty(attr.ConditionedOn);
        //                        if (String.IsNullOrWhiteSpace(iniValue))
        //                        {
        //                            conditionField.SetValue(obj, false);
        //                        }
        //                        else
        //                        {
        //                            conditionField.SetValue(obj, true);
        //                        }
        //                    }

        //                    if (String.IsNullOrWhiteSpace(iniValue))
        //                    {
        //                        // Skip non-string values which are not found
        //                        continue;
        //                    }

        //                    var valueSet = StringUtils.SetPropertyValue(iniValue, obj, field, attr);
        //                    if (!valueSet)
        //                        throw new ArgumentException(String.Format("Unexpected field type {0} for INI key {1} in section {2}.", fieldType.ToString(), keyName, attr.Section));
        //                }
        //            }
        //        }
        //    }
        //}

        //public void Serialize(object obj)
        //{
        //    var fields = obj.GetType().GetProperties().Where(f => f.IsDefined(typeof(IniFileEntryAttribute), false));
        //    foreach (var field in fields)
        //    {
        //        var attributes = field.GetCustomAttributes(typeof(IniFileEntryAttribute), false);
        //        foreach (var attribute in attributes)
        //        {
        //            var attr = attribute as IniFileEntryAttribute;
        //            var value = field.GetValue(obj);
        //            var keyName = String.IsNullOrWhiteSpace(attr.Key) ? field.Name : attr.Key;

        //            if (attr.ClearSection)
        //            {
        //                IniWriteValue(IniFiles.GameUserSettings, attr.Section, null, null);
        //            }

        //            //
        //            // If this is a collection, we need to first remove all of its values from the INI.
        //            //
        //            IIniValuesCollection collection = value as IIniValuesCollection;
        //            if (collection != null)
        //            {
        //                var filteredSection = IniReadSection(attr.File, attr.Section)
        //                                            .Where(s => !s.StartsWith(collection.IniCollectionKey + (collection.IsArray ? "[" : "=")))
        //                                            .ToArray();
        //                IniWriteSection(attr.File, attr.Section, filteredSection);
        //            }

        //            if (!String.IsNullOrEmpty(attr.ConditionedOn))
        //            {
        //                var conditionField = obj.GetType().GetProperty(attr.ConditionedOn);
        //                var conditionValue = conditionField.GetValue(obj);
        //                if (conditionValue is bool && (bool)conditionValue == false)
        //                {
        //                    // The condition value was not set to true, so clear this attribute instead of writing it
        //                    IniWriteValue(attr.File, attr.Section, keyName, null);
        //                    continue;
        //                }
        //            }

        //            if (!String.IsNullOrEmpty(attr.ClearWhenOff))
        //            {
        //                var updateOffField = obj.GetType().GetProperty(attr.ClearWhenOff);
        //                var updateOffValue = updateOffField.GetValue(obj);
        //                if (updateOffValue is bool && (bool)updateOffValue == false)
        //                {
        //                    // The attributed value was set to false, so clear this attribute instead of writing it
        //                    IniWriteValue(attr.File, attr.Section, keyName, null);
        //                }
        //                continue;
        //            }

        //            if (attr.WriteBoolValueIfNonEmpty)
        //            {
        //                if (value == null)
        //                {
        //                    IniWriteValue(attr.File, attr.Section, keyName, "False");
        //                }
        //                else
        //                {
        //                    if (value is string)
        //                    {
        //                        var strValue = value as string;
        //                        IniWriteValue(attr.File, attr.Section, keyName, String.IsNullOrEmpty(strValue) ? "False" : "True");
        //                    }
        //                    else
        //                    {
        //                        // Not supported
        //                        throw new NotSupportedException("Unexpected IniFileEntry value type.");
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                if (collection != null)
        //                {
        //                    if (collection.IsEnabled)
        //                    {
        //                        // Remove all the values in the collection with this key name
        //                        var filteredSection = collection.IsArray ? IniReadSection(attr.File, attr.Section).Where(s => !s.StartsWith(keyName + "["))
        //                                                                 : IniReadSection(attr.File, attr.Section).Where(s => !s.StartsWith(keyName + "="));
        //                        var result = filteredSection.Concat(collection.ToIniValues()).ToArray();
        //                        IniWriteSection(attr.File, attr.Section, result);
        //                    }
        //                }
        //                else
        //                {
        //                    var strValue = StringUtils.GetPropertyValue(value, field, attr);
        //                    if (attr.QuotedString && !(strValue.StartsWith("\"") && strValue.EndsWith("\"")))
        //                    {
        //                        strValue = "\"" + strValue + "\"";
        //                    }

        //                    if (attr.Multiline)
        //                    {
        //                        // substitutes the NewLine string with "\n"
        //                        strValue = strValue.Replace(Environment.NewLine, @"\n");
        //                    }

        //                    IniWriteValue(attr.File, attr.Section, keyName, strValue);
        //                }
        //            }
        //        }
        //    }
        //}

        //private string[] IniReadSection(IniFiles iniFile, IniFileSections section)
        //{
        //    var file = Path.Combine(this.basePath, FileNames[iniFile]);
        //    return IniFileUtils.IniReadSection(file, SectionNames[section]);
        //}

        //private string IniReadValue(IniFiles iniFile, IniFileSections section, string keyName)
        //{
        //    var file = Path.Combine(this.basePath, FileNames[iniFile]);
        //    return IniFileUtils.IniReadValue(file, SectionNames[section], keyName, string.Empty);
        //}

        //private void IniWriteSection(IniFiles iniFile, IniFileSections section, string[] values)
        //{
        //    var file = Path.Combine(this.basePath, FileNames[iniFile]);
        //    IniFileUtils.IniWriteSection(file, SectionNames[section], values);
        //}

        //private void IniWriteValue(IniFiles iniFile, IniFileSections section, string keyName, string keyValue)
        //{
        //    var file = Path.Combine(this.basePath, FileNames[iniFile]);
        //    IniFileUtils.IniWriteValue(file, SectionNames[section], keyName, keyValue);
        //}
    }
}
