﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;
using UnityEngine;

namespace BDArmory.Core
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BDAPersistantSettingsField : Attribute
    {
        public BDAPersistantSettingsField()
        {
        }

        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);

            if (!fileNode.HasNode("BDASettings"))
            {
                fileNode.AddNode("BDASettings");
            }

            ConfigNode settings = fileNode.GetNode("BDASettings");
            IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator();
            while (field.MoveNext())
            {
                if (field.Current == null) continue;
                if (!field.Current.IsDefined(typeof(BDAPersistantSettingsField), false)) continue;

                var fieldValue = field.Current.GetValue(null);
                if (fieldValue.GetType() == typeof(Vector2d))
                    settings.SetValue(field.Current.Name, ((Vector2d)fieldValue).ToString("G"), true);
                else
                {
                    settings.SetValue(field.Current.Name, field.Current.GetValue(null).ToString(), true);
                }
            }
            field.Dispose();
            fileNode.Save(BDArmorySettings.settingsConfigURL);
        }

        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
            if (!fileNode.HasNode("BDASettings")) return;

            ConfigNode settings = fileNode.GetNode("BDASettings");

            IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator();
            while (field.MoveNext())
            {
                if (field.Current == null) continue;
                if (!field.Current.IsDefined(typeof(BDAPersistantSettingsField), false)) continue;

                if (!settings.HasValue(field.Current.Name)) continue;
                object parsedValue = ParseValue(field.Current.FieldType, settings.GetValue(field.Current.Name));
                if (parsedValue != null)
                {
                    field.Current.SetValue(null, parsedValue);
                }
            }
            field.Dispose();
        }

        public static object ParseValue(Type type, string value)
        {
            try
            {
                if (type == typeof(string))
                {
                    return value;
                }

                if (type == typeof(bool))
                {
                    return Boolean.Parse(value);
                }
                else if (type.IsEnum)
                {
                    return System.Enum.Parse(type, value);
                }
                else if (type == typeof(float))
                {
                    return Single.Parse(value);
                }
                else if (type == typeof(int))
                {
                    return int.Parse(value);
                }
                else if (type == typeof(Single))
                {
                    return Single.Parse(value);
                }
                else if (type == typeof(Rect))
                {
                    string[] strings = value.Split(',');
                    int xVal = Int32.Parse(strings[0].Split(':')[1].Split('.')[0]);
                    int yVal = Int32.Parse(strings[1].Split(':')[1].Split('.')[0]);
                    int wVal = Int32.Parse(strings[2].Split(':')[1].Split('.')[0]);
                    int hVal = Int32.Parse(strings[3].Split(':')[1].Split('.')[0]);
                    Rect rectVal = new Rect
                    {
                        x = xVal,
                        y = yVal,
                        width = wVal,
                        height = hVal
                    };
                    return rectVal;
                }
                else if (type == typeof(Vector2d))
                {
                    char[] charsToTrim = { '(', ')', ' ' };
                    string[] strings = value.Trim(charsToTrim).Split(',');
                    double x = double.Parse(strings[0]);
                    double y = double.Parse(strings[1]);
                    return new Vector2d(x, y);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[BDArmory.BDAPersistantSettingsField]: Failed to parse '" + value + "' as a " + type.ToString() + ": " + e.Message);
                return null;
            }
            Debug.LogError("[BDArmory.BDAPersistantSettingsField]: BDAPersistantSettingsField to parse settings field of type " + type + " and value " + value);
            return null;
        }
    }
}
