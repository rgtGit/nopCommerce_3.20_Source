﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Nop.Web.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.5.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\Projects\\Parts\\nopCommerce_3.20_Source\\Presentation\\Nop.Web\\Views\\Checkout\\Ema" +
            "ilConfirmation_Header.html")]
        public string Path_EmailConfirmatuon_Header {
            get {
                return ((string)(this["Path_EmailConfirmatuon_Header"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\Projects\\Parts\\nopCommerce_3.20_Source\\Presentation\\Nop.Web\\Views\\Checkout\\Ema" +
            "ilConfirmation_Detail.html")]
        public string Path_EmailConfirmatuon_Detail {
            get {
                return ((string)(this["Path_EmailConfirmatuon_Detail"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\Projects\\Parts\\nopCommerce_3.20_Source\\Presentation\\Nop.Web\\Views\\Checkout\\Ema" +
            "ilConfirmation_Footer.html")]
        public string Path_EmailConfirmatuon_Footer {
            get {
                return ((string)(this["Path_EmailConfirmatuon_Footer"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("C:\\Projects\\Parts\\nopCommerce_3.20_Source\\Presentation\\Nop.Web\\Content\\Images\\Thu" +
            "mbs\\")]
        public string Parts_ImagePath {
            get {
                return ((string)(this["Parts_ImagePath"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://parts.masterspasportal.com/images/")]
        public string Parts_ImagePathProd {
            get {
                return ((string)(this["Parts_ImagePathProd"]));
            }
        }
    }
}