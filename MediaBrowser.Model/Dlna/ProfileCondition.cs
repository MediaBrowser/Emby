﻿using System.Xml.Serialization;

namespace MediaBrowser.Model.Dlna
{
    public class ProfileCondition
    {
        public ProfileConditionType Condition { get; set; }

        public ProfileConditionValue Property { get; set; }

        public string Value { get; set; }

        public bool IsRequired { get; set; }

        public ProfileCondition()
        {
            IsRequired = true;
        }

        public ProfileCondition(ProfileConditionType condition, ProfileConditionValue property, string value)
            : this(condition, property, value, false)
        {
            
        }

        public ProfileCondition(ProfileConditionType condition, ProfileConditionValue property, string value, bool isRequired)
        {
            Condition = condition;
            Property = property;
            Value = value;
            IsRequired = isRequired;
        }
    }
}