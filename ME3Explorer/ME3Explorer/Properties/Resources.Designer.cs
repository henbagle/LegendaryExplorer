﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ME3Explorer.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("ME3Explorer.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        public static byte[] KismetFont {
            get {
                object obj = ResourceManager.GetObject("KismetFont", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to // This has to match the data in the vertex buffer.
        ///struct VS_IN {
        ///	float3 pos : POSITION;
        ///	float3 normal : NORMAL;
        ///	float2 uv : TEXCOORD0;
        ///};
        ///
        ///struct VS_OUT {
        ///	float4 pos : SV_POSITION;
        ///	float3 normal : NORMAL;
        ///	float2 uv : TEXCOORD0;
        ///};
        ///
        ///struct PS_IN {
        ///	float4 pos : SV_POSITION;
        ///	float3 normal : NORMAL;
        ///	float2 uv : TEXCOORD0;
        ///};
        ///
        ///struct PS_OUT {
        ///	float4 color : SV_TARGET;
        ///};
        ///
        ///cbuffer constants {
        ///	float4x4 projection;
        ///	float4x4 view;
        ///	float4x4 model;
        ///};
        ///
        ///Texture2D tex : regist [rest of string was truncated]&quot;;.
        /// </summary>
        public static string StandardShader {
            get {
                return ResourceManager.GetString("StandardShader", resourceCulture);
            }
        }
    }
}
