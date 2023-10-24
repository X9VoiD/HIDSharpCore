﻿#region License
/* Copyright 2012-2015, 2017 James F. Bellinger <http://www.zer7.com/software/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License. */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using HidSharp.Exceptions;

namespace HidSharp.Platform.Linux
{
    using static HidSharp.Platform.Linux.NativeMethods;

    sealed class LinuxHidDevice : HidDevice
    {
        object _getInfoLock;
        string _manufacturer;
        string _productName;
        string _serialNumber;
        byte[] _reportDescriptor;
        int _vid, _pid, _version;
        int _maxInput, _maxOutput, _maxFeature;
        bool _reportsUseID;
        string _path, _fileSystemName;

        LinuxHidDevice()
        {
            _getInfoLock = new object();
        }

        internal static LinuxHidDevice TryCreate(string path)
        {
            var d = new LinuxHidDevice() { _path = path };

            IntPtr udev = NativeMethodsLibudev.Instance.udev_new();
            if (IntPtr.Zero != udev)
            {
                try
                {
                    IntPtr device = NativeMethodsLibudev.Instance.udev_device_new_from_syspath(udev, d._path);
                    if (device != IntPtr.Zero)
                    {
                        try
                        {
                            string devnode = NativeMethodsLibudev.Instance.udev_device_get_devnode(device);
                            if (devnode != null)
                            {
                                d._fileSystemName = devnode;

                                //if (NativeMethodsLibudev.Instance.udev_device_get_is_initialized(device) > 0)
                                {
                                    IntPtr parent = NativeMethodsLibudev.Instance.udev_device_get_parent_with_subsystem_devtype(device, "usb", "usb_device");
                                    if (IntPtr.Zero != parent)
                                    {
                                        string manufacturer = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "manufacturer");
                                        string productName = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "product");
                                        string serialNumber = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "serial");
                                        string idVendor = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idVendor");
                                        string idProduct = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "idProduct");
                                        string bcdDevice = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parent, "bcdDevice");

                                        int vid, pid, version;
                                        if (NativeMethods.TryParseHex(idVendor, out vid) &&
                                            NativeMethods.TryParseHex(idProduct, out pid) &&
                                            NativeMethods.TryParseHex(bcdDevice, out version))
                                        {
                                            d._vid = vid;
                                            d._pid = pid;
                                            d._version = version;
                                            d._manufacturer = manufacturer;
                                            d._productName = productName;
                                            d._serialNumber = serialNumber;

                                            return d;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            NativeMethodsLibudev.Instance.udev_device_unref(device);
                        }
                    }
                }
                finally
                {
                    NativeMethodsLibudev.Instance.udev_unref(udev);
                }
            }

            return null;
        }

        protected override DeviceStream OpenDeviceDirectly(OpenConfiguration openConfig)
        {
            RequiresGetInfo();

            var stream = new LinuxHidStream(this);
            try { stream.Init(_path); return stream; }
            catch { stream.Close(); throw; }
        }

        public override string GetManufacturer()
        {
            if (_manufacturer == null) { throw DeviceException.CreateIOException(this, "Unnamed manufacturer."); }
            return _manufacturer;
        }

        public override string GetProductName()
        {
            if (_productName == null) { throw DeviceException.CreateIOException(this, "Unnamed product."); }
            return _productName;
        }

        public override string GetSerialNumber()
        {
            if (_serialNumber == null) { throw DeviceException.CreateIOException(this, "No serial number."); }
            return _serialNumber;
        }

        public override int GetMaxInputReportLength()
        {
            RequiresGetInfo();
            return _maxInput;
        }

        public override int GetMaxOutputReportLength()
        {
            RequiresGetInfo();
            return _maxOutput;
        }

        public override int GetMaxFeatureReportLength()
        {
            RequiresGetInfo();
            return _maxFeature;
        }

        public override byte[] GetRawReportDescriptor()
        {
            RequiresGetInfo();
            return (byte[])_reportDescriptor.Clone();
        }

        public override unsafe string GetDeviceString(int index)
        {
            static ushort string_index(byte index)
            {
                return (ushort)((((byte)DESCRIPTOR_TYPE.STRING) << 8) | index);
            }

            // Setup the packet for retrieving supported langId
            usbfs_ctrltransfer setup = new usbfs_ctrltransfer
            {
                bRequestType = (byte)ENDPOINT_DIRECTION.IN,
                bRequest = (byte)STANDARD_REQUEST.GET_DESCRIPTOR,
                wValue = string_index(0),
                wIndex = 0,
                wLength = 255
            };

            string usbPath = GetUsbPath();

            fixed (char* sbuf = new char[255])
            {
                setup.data = sbuf;

                // Send packet
                int usbHandle = open(usbPath, oflag.NONBLOCK | oflag.RDWR);

                try
                {
                    if (ioctl(usbHandle, USBDEVFS_CONTROL, ref setup) < 0)
                    {
                        close(usbHandle);
                        var err = (error)Marshal.GetLastWin32Error();
                        throw new DeviceIOException(this, $"Unable to retrieve device's supported langId: {err}");
                    }

                    // Retrieve langId
                    var buf = (byte*)setup.data;
                    ushort langId = (ushort)(buf[2] | buf[3] << 8);

                    for (int i = 0; i < 255; i++)
                    {
                        buf[i] = 0;
                    }

                    // Retrieve string
                    setup.wIndex = langId;
                    setup.wValue = string_index((byte)index);
                    if (ioctl(usbHandle, USBDEVFS_CONTROL, ref setup) < 0)
                    {
                        var err = (error)Marshal.GetLastWin32Error();
                        throw new DeviceIOException(this, $"Unable to retrieve device string at index {index}: {err}");
                    }
                }
                finally
                {
                    close(usbHandle);
                }

                var deviceString = new StringBuilder(255);
                var ssbuf = (char*)setup.data;
                for (int i = 1; i < 255; i++)
                {
                    var c = ssbuf[i];
                    if (c == 0)
                        break;
                    else
                        deviceString.Append(c);
                }
                return deviceString.ToString();
            }
        }

        bool TryParseReportDescriptor(out Reports.ReportDescriptor parser, out byte[] reportDescriptor)
        {
            parser = null; reportDescriptor = null;

            int handle;
            try { handle = LinuxHidStream.DeviceHandleFromPath(_path, this, NativeMethods.oflag.NONBLOCK); }
            catch (FileNotFoundException) { throw DeviceException.CreateIOException(this, "Failed to read report descriptor."); }

            try
            {
                uint descsize;
                if (NativeMethods.ioctl(handle, NativeMethods.HIDIOCGRDESCSIZE, out descsize) < 0) { return false; }
                if (descsize > NativeMethods.HID_MAX_DESCRIPTOR_SIZE) { return false; }

                var desc = new NativeMethods.hidraw_report_descriptor() { size = descsize };
                if (NativeMethods.ioctl(handle, NativeMethods.HIDIOCGRDESC, ref desc) < 0) { return false; }

                Array.Resize(ref desc.value, (int)descsize);
                parser = new Reports.ReportDescriptor(desc.value);
                reportDescriptor = desc.value; return true;
            }
            finally
            {
                NativeMethods.retry(() => NativeMethods.close(handle));
            }
        }

        void RequiresGetInfo()
        {
            lock (_getInfoLock)
            {
                if (_reportDescriptor != null) { return; }

                Reports.ReportDescriptor parser; byte[] reportDescriptor;
                if (!TryParseReportDescriptor(out parser, out reportDescriptor))
                {
                    throw DeviceException.CreateIOException(this, "Failed to read report descriptor.");
                }

                _maxInput = parser.MaxInputReportLength;
                _maxOutput = parser.MaxOutputReportLength;
                _maxFeature = parser.MaxFeatureReportLength;
                _reportsUseID = parser.ReportsUseID;
                _reportDescriptor = reportDescriptor;
            }
        }

        unsafe string GetUsbPath()
        {
            using (var udev = new SafeUdevHandle(NativeMethodsLibudev.Instance.udev_new()))
            using (var handle = new SafeUdevDeviceHandle(NativeMethodsLibudev.Instance.udev_device_new_from_syspath(udev.DangerousGetHandle(), _path)))
            using (var parent = new SafeUdevDeviceHandle(NativeMethodsLibudev.Instance.udev_device_get_parent_with_subsystem_devtype(handle.DangerousGetHandle(), "usb", "usb_device")))
            {
                var parentPtr = parent.DangerousGetHandle();
                string devNum = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parentPtr, "devnum");
                string busNum = NativeMethodsLibudev.Instance.udev_device_get_sysattr_value(parentPtr, "busnum");

                return $"/dev/bus/usb/{int.Parse(busNum):D3}/{int.Parse(devNum):D3}";
            }
        }

        public override string GetFileSystemName()
        {
            return _fileSystemName;
        }

        public override bool HasImplementationDetail(Guid detail)
        {
            return base.HasImplementationDetail(detail) || detail == ImplementationDetail.Linux || detail == ImplementationDetail.HidrawApi;
        }

        public override bool IsSibling(HidDevice device)
        {
            if (device is not LinuxHidDevice linuxDevice) { return false; }

            try
            {
                return GetUsbPath() == linuxDevice.GetUsbPath();
            }
            catch
            {
                return false;
            }
        }

        public override string DevicePath
        {
            get { return _path; }
        }

        public override int VendorID
        {
            get { return _vid; }
        }

        public override int ProductID
        {
            get { return _pid; }
        }

        public override int ReleaseNumberBcd
        {
            get { return _version; }
        }

        public override bool CanOpen => true;

        internal bool ReportsUseID
        {
            get { return _reportsUseID; }
        }
    }
}
