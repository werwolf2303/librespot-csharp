// This file was generated by a tool; you should avoid making direct changes.
// Consider using 'partial classes' to extend these types
// Input: connectivity.proto

#pragma warning disable 0612, 1591, 3021
namespace spotify.clienttoken.data.v0
{

    [global::ProtoBuf.ProtoContract()]
    public partial class ConnectivitySdkData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public ConnectivitySdkData()
        {
            DeviceId = "";
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"platform_specific_data")]
        public PlatformSpecificData PlatformSpecificData { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"device_id")]
        [global::System.ComponentModel.DefaultValue("")]
        public string DeviceId { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PlatformSpecificData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public PlatformSpecificData()
        {
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"android")]
        public NativeAndroidData Android
        {
            get { return __pbn__data.Is(1) ? ((NativeAndroidData)__pbn__data.Object) : default(NativeAndroidData); }
            set { __pbn__data = new global::ProtoBuf.DiscriminatedUnionObject(1, value); }
        }
        public bool ShouldSerializeAndroid()
        {
            return __pbn__data.Is(1);
        }
        public void ResetAndroid()
        {
            global::ProtoBuf.DiscriminatedUnionObject.Reset(ref __pbn__data, 1);
        }

        private global::ProtoBuf.DiscriminatedUnionObject __pbn__data;

        [global::ProtoBuf.ProtoMember(2, Name = @"ios")]
        public NativeIOSData Ios
        {
            get { return __pbn__data.Is(2) ? ((NativeIOSData)__pbn__data.Object) : default(NativeIOSData); }
            set { __pbn__data = new global::ProtoBuf.DiscriminatedUnionObject(2, value); }
        }
        public bool ShouldSerializeIos()
        {
            return __pbn__data.Is(2);
        }
        public void ResetIos()
        {
            global::ProtoBuf.DiscriminatedUnionObject.Reset(ref __pbn__data, 2);
        }

        [global::ProtoBuf.ProtoMember(4, Name = @"windows")]
        public NativeWindowsData Windows
        {
            get { return __pbn__data.Is(4) ? ((NativeWindowsData)__pbn__data.Object) : default(NativeWindowsData); }
            set { __pbn__data = new global::ProtoBuf.DiscriminatedUnionObject(4, value); }
        }
        public bool ShouldSerializeWindows()
        {
            return __pbn__data.Is(4);
        }
        public void ResetWindows()
        {
            global::ProtoBuf.DiscriminatedUnionObject.Reset(ref __pbn__data, 4);
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class NativeAndroidData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public NativeAndroidData()
        {
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"major_sdk_version")]
        public int MajorSdkVersion { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"minor_sdk_version")]
        public int MinorSdkVersion { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"patch_sdk_version")]
        public int PatchSdkVersion { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"api_version")]
        public uint ApiVersion { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"screen_dimensions")]
        public Screen ScreenDimensions { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class NativeIOSData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public NativeIOSData()
        {
            HwMachine = "";
            SystemVersion = "";
            SimulatorModelIdentifier = "";
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"user_interface_idiom")]
        public int UserInterfaceIdiom { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"target_iphone_simulator")]
        public bool TargetIphoneSimulator { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"hw_machine")]
        [global::System.ComponentModel.DefaultValue("")]
        public string HwMachine { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"system_version")]
        [global::System.ComponentModel.DefaultValue("")]
        public string SystemVersion { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"simulator_model_identifier")]
        [global::System.ComponentModel.DefaultValue("")]
        public string SimulatorModelIdentifier { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class NativeWindowsData : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public NativeWindowsData()
        {
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"something1")]
        public int Something1 { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"something3")]
        public int Something3 { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"something4")]
        public int Something4 { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"something6")]
        public int Something6 { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"something7")]
        public int Something7 { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"something8")]
        public int Something8 { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"something10")]
        public bool Something10 { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class Screen : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        {
            return global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);
        }
        public Screen()
        {
            OnConstructor();
        }

        partial void OnConstructor();

        [global::ProtoBuf.ProtoMember(1, Name = @"width")]
        public int Width { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"height")]
        public int Height { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"density")]
        public int Density { get; set; }

    }

}

#pragma warning restore 0612, 1591, 3021
