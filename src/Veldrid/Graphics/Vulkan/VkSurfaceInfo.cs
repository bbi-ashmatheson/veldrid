﻿using System;
using Vulkan;
using Vulkan.Xlib;
using static Veldrid.Graphics.Vulkan.VulkanUtil;
using static Vulkan.VulkanNative;

namespace Veldrid.Graphics.Vulkan
{
    public abstract class VkSurfaceSource
    {
        public abstract VkSurfaceKHR CreateSurface(VkInstance instance);

        public static VkSurfaceSource CreateWin32(IntPtr hinstance, IntPtr hwnd) => new Win32VkSurfaceInfo(hinstance, hwnd);
        public unsafe static VkSurfaceSource CreateXlib(Display* display, Window window) => new XlibVkSurfaceInfo(display, window);
    }

    internal class Win32VkSurfaceInfo : VkSurfaceSource
    {
        private readonly IntPtr _hinstance;
        private readonly IntPtr _hwnd;

        public Win32VkSurfaceInfo(IntPtr hinstance, IntPtr hwnd)
        {
            _hinstance = hinstance;
            _hwnd = hwnd;
        }

        public unsafe override VkSurfaceKHR CreateSurface(VkInstance instance)
        {
            VkWin32SurfaceCreateInfoKHR surfaceCI = VkWin32SurfaceCreateInfoKHR.New();
            surfaceCI.hwnd = _hwnd;
            surfaceCI.hinstance = _hinstance;
            CheckResult(vkCreateWin32SurfaceKHR(instance, ref surfaceCI, null, out VkSurfaceKHR surface));
            return surface;
        }
    }

    internal class XlibVkSurfaceInfo : VkSurfaceSource
    {
        private readonly unsafe Display* _display;
        private readonly Window _window;

        public unsafe XlibVkSurfaceInfo(Display* display, Window window)
        {
            _display = display;
            _window = window;
        }

        public unsafe override VkSurfaceKHR CreateSurface(VkInstance instance)
        {
            VkXlibSurfaceCreateInfoKHR xsci = VkXlibSurfaceCreateInfoKHR.New();
            xsci.dpy = _display;
            xsci.window = _window;
            CheckResult(vkCreateXlibSurfaceKHR(instance, ref xsci, null, out VkSurfaceKHR surface));
            return surface;
        }
    }
}
