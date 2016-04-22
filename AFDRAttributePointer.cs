// Copyright (C) 2008-2010 OSIsoft, LLC. All rights reserved.
// THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND,
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE OR NONINFRINGEMENT.
// Modified by Fabiano Batista
// Code derived from String Concat Custom Data Reference

using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Data;
using OSIsoft.AF.EventFrame;
using OSIsoft.AF.UnitsOfMeasure;
using OSIsoft.AF.Time;

namespace OSIsoft.AF.Asset.DataReference
{
    // Implementation of the data reference
    [Serializable]
    [Guid("567240DA-AB97-4A33-B78F-C3B24944AD81")]
    [Description("Pointer;Used to obtain values from pointed attributes.")]
    public class AFDRAttributePointer : AFDataReference
    {
        private string configString = String.Empty;

        public AFDRAttributePointer()
            : base()
        {
        }

        #region Implementation of AFDataReference
        public override AFDataReferenceContext SupportedContexts
        {
            get
            {
                return (AFDataReferenceContext.All);
            }
        }

        public override AFDataReferenceMethod SupportedMethods
        {
            get
            {
                AFDataReferenceMethod supportedMethods =
                    AFDataReferenceMethod.GetValue | AFDataReferenceMethod.GetValues;
                return supportedMethods;
            }
        }

        public override AFDataMethods SupportedDataMethods
        {
            get
            {
                return base.DefaultSupportedDataMethods;
            }
        }

        public override string ConfigString
        {
            // The ConfigString property is used to store and load the configuration of this data reference.
            get
            {
                return configString;
            }
            set
            {
                if (ConfigString != value)
                {
                    if (value != null)
                        configString = value.Trim();

                    // notify SDK and clients of change.  Required to have changes saved.
                    SaveConfigChanges();

                    CheckDataType();
                }
            }
        }

        public override AFAttributeList GetInputs(object context)
        {
            // Loop through the config string, looking for attributes
            // The Config string is semicolon separated list of attributes and strings
            // Strings must be enclosed in " "
            // Will also handle standard AF substitions (%ELEMENT%, %TIME%, etc.)

            AFAttributeList paramAttributes = null;
            StringBuilder sbComposedAttrName = new StringBuilder();
            string[] subStrings = ConfigString.Split(';');
            for (int i = 0; i < subStrings.Length; i++)
            {
                string s = subStrings[i].Trim();
                String subst = SubstituteParameters(s, this, context, null);
                if (!String.IsNullOrEmpty(subst) && !subst.StartsWith("\""))
                {
                    // Get attribute will resolve attribute references 
                    AFAttribute attr;
                    if (!s.StartsWith("%")) //to prevent trying to resolve searching an attribute name on system-related substr. parameters
                    {
                        if (base.Attribute.Name != subst) //to avoid circular reference
                        {

                            attr = base.GetAttribute(subst);

                            if (attr == null || attr.IsDeleted)
                            {
                                throw new ApplicationException(String.Format("Unknown attribute '{0}'", subst));
                            }

                            AFValue avParameterAttrValue;

                            String strParameterAttrValue = "";
                            try
                            {
                                avParameterAttrValue = attr.GetValue();
                                strParameterAttrValue = avParameterAttrValue.ToString();
                            }
                            catch
                            {
                                strParameterAttrValue = "Unkown Attribute"; 
                                throw;

                            }

                            if (avParameterAttrValue.Value == null || !avParameterAttrValue.IsGood)
                            {
                                throw new ArgumentException(
                                    String.Format("Bad input value in '{0}': {1}",
                                    s, strParameterAttrValue));

                            }

                            try
                            {
                                sbComposedAttrName.Append(strParameterAttrValue);
                            }
                            catch
                            {
                                throw;
                            }




                           

                        }
                        else
                        {
                            throw new ApplicationException("Circular reference detected: you cannot use a reference to the current attribute.");
                        }

                    }
                    else
                    {
                        sbComposedAttrName.Append(subst);
                    }
                }
                else
                {
                    sbComposedAttrName.Append(s.Substring(1).TrimEnd('"')); //removes "" from the substring
                    
                }

            }

            String strComposedAttrName = sbComposedAttrName.ToString();
            AFAttribute attrComposed;
            if (strComposedAttrName !="")
            {
               
                attrComposed = base.GetAttribute(strComposedAttrName);
                if (attrComposed == null || attrComposed.IsDeleted)
                {
                    throw new ApplicationException(String.Format("Unknown attribute '{0}'", strComposedAttrName));
                }
                else
                {
                    
                    if (paramAttributes == null)
                        paramAttributes = new AFAttributeList();
                    paramAttributes.Add(attrComposed);
                    

                }

            }

            return paramAttributes;
        }

  
        public override AFValue GetValue(object context, object timeContext, AFAttributeList inputAttributes, AFValues inputValues)
        {
            // Evaluate
            AFTime timestamp = AFTime.MinValue;
            if (inputValues != null)
                    {
                        AFValue v = inputValues[0];
                        if (v.Timestamp > timestamp)
                            timestamp = v.Timestamp;

                        if (v.Value == null || !v.IsGood)
                        {
                            AFValue badValue = new AFValue(
                                String.Format("Bad input value in '{0}': {1}",
                                inputAttributes[0].Name, v.Value), 
                                timestamp, null, AFValueStatus.Bad);
                            return badValue; // just return bad value
                        }

                                          }
                    else
                    {
                        return new AFValue(
                            "Invalid data sent to GetValue", timestamp, null, AFValueStatus.Bad);
                    }


            // should be returning effective date as absolute minimum
            if (timestamp.IsEmpty && Attribute != null)
            {
                if (Attribute.Element is IAFVersionable)
                    timestamp = ((IAFVersionable)Attribute.Element).Version.EffectiveDate;
                else if (Attribute.Element is AFEventFrame)
                    timestamp = ((AFEventFrame)Attribute.Element).StartTime;
            }
            else if (timestamp.IsEmpty && timeContext is AFTime)
                timestamp = (AFTime)timeContext;



            AFValue finalAttrValue = inputAttributes[0].GetValue(timestamp);

            
            return finalAttrValue;
        }
        #endregion

   
         public override AFValues GetValues(object context, AFTimeRange timeContext, int numberOfValues, AFAttributeList inputAttributes, AFValues[] inputValues)
        {

            // Evaluate
            try
            {
                // base implementation is sufficient for all calculation data references except when no inputs
                if (numberOfValues > 0 && (inputValues == null || inputValues.Length == 0))
                {
                    // when no inputs on a plot values call, just return the value at the start time
                    AFValues values = new AFValues();
                    values.Add(GetValue(context, timeContext.StartTime, inputAttributes, null));
                    return values;
                }
                return base.GetValues(context, timeContext, numberOfValues, inputAttributes, inputValues);
            }
            catch
            {
                // For any exception, unload parameters and set flag so parameters
                //  are rechecked on the next call.
                throw;
            }
		}


        // Since base property 'IsInitializing' only exists in AF 2.1 or later, must
        //  separate the call into the following two methods because an exception is
        //  thrown when 'BaseIsInitializing' is compiled by the CLR.
        //  This would only occur when a AF 2.0 client connects to an AFServer 2.1.
        private bool CheckIsInitializing()
        {
            try
            {
                return BaseIsInitializing();
            }
            catch { }
            return false;
        }
        private bool BaseIsInitializing()
        {
            return IsInitializing;
        }

        internal void CheckDataType()
        {
            if (CheckIsInitializing()) return;
            if (Attribute != null && Attribute.Template != null) return; // can't do anything
            // check to see we are already dirty
            if (Attribute != null && Attribute.Element is IAFTransactable && !((IAFTransactable)Attribute.Element).IsDirty) return;
            if (Template != null && !Template.ElementTemplate.IsDirty) return;

            Type type = null;
            if (Attribute != null)
                type = Attribute.Type;
            else if (Template != null)
                type = Template.Type;
           
        }
    }
}
