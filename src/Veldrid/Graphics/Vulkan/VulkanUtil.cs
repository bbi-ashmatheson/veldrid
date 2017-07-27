﻿using System.Diagnostics;
using Vulkan;
using static Vulkan.VulkanNative;

namespace Veldrid.Graphics.Vulkan
{
    internal unsafe static class VulkanUtil
    {
        [Conditional("DEBUG")]
        public static void CheckResult(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new VeldridException("Unsuccessful VkResult: " + result);
            }
        }

        public static uint FindMemoryType(VkPhysicalDevice physicalDevice, uint typeFilter, VkMemoryPropertyFlags properties)
        {
            vkGetPhysicalDeviceMemoryProperties(physicalDevice, out VkPhysicalDeviceMemoryProperties memProperties);
            for (int i = 0; i < memProperties.memoryTypeCount; i++)
            {
                if (((typeFilter & (1 << i)) != 0)
                    && (memProperties.GetMemoryType((uint)i).propertyFlags & properties) == properties)
                {
                    return (uint)i;
                }
            }

            throw new VeldridException("No suitable memory type.");
        }

        public static void CreateImage(
            VkDevice device,
            VkPhysicalDevice physicalDevice,
            VkDeviceMemoryManager memoryManager,
            uint width,
            uint height,
            uint arrayLayers,
            VkFormat format,
            VkImageTiling tiling,
            VkImageUsageFlags usage,
            VkMemoryPropertyFlags properties,
            out VkImage image,
            out VkMemoryBlock memory)
        {
            VkImageCreateInfo imageCI = VkImageCreateInfo.New();
            imageCI.imageType = VkImageType.Image2D;
            imageCI.extent.width = width;
            imageCI.extent.height = height;
            imageCI.extent.depth = 1;
            imageCI.mipLevels = 1;
            imageCI.arrayLayers = arrayLayers;
            imageCI.format = format;
            imageCI.tiling = tiling;
            imageCI.initialLayout = VkImageLayout.Preinitialized;
            imageCI.usage = usage;
            imageCI.sharingMode = VkSharingMode.Exclusive;
            imageCI.samples = VkSampleCountFlags.Count1;

            VkResult result = vkCreateImage(device, ref imageCI, null, out image);
            CheckResult(result);

            vkGetImageMemoryRequirements(device, image, out VkMemoryRequirements memRequirements);
            VkMemoryBlock memoryToken = memoryManager.Allocate(
                FindMemoryType(physicalDevice, memRequirements.memoryTypeBits, properties),
                memRequirements.size,
                memRequirements.alignment);
            memory = memoryToken;
            result = vkBindImageMemory(device, image, memory.DeviceMemory, memory.Offset);
            CheckResult(result);
        }
    }

    internal unsafe static class VkPhysicalDeviceMemoryPropertiesEx
    {
        public static VkMemoryType GetMemoryType(this VkPhysicalDeviceMemoryProperties memoryProperties, uint index)
        {
            return (&memoryProperties.memoryTypes_0)[index];
        }
    }
}
