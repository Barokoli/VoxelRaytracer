﻿using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;
using System;

static unsafe class Unmanaged
{
    public static void* New<T>(int elementCount)
        where T : struct
    {
        return Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)) *
					elementCount).ToPointer();
    }

    public static void* NewAndInit<T>(int elementCount)
        where T : struct
    {
        int newSizeInBytes = Marshal.SizeOf(typeof(T)) * elementCount;
        byte* newArrayPointer =
		(byte*) Marshal.AllocHGlobal(newSizeInBytes).ToPointer();

        for (int i = 0; i < newSizeInBytes; i++)
            *(newArrayPointer + i) = 0;

        return (void*) newArrayPointer;
    }

    public static void Free(void* pointerToUnmanagedMemory)
    {
        Marshal.FreeHGlobal(new IntPtr(pointerToUnmanagedMemory));
    }

    public static void* Resize<T>(void* oldPointer, int newElementCount)
        where T : struct
    {
        return (Marshal.ReAllocHGlobal(new IntPtr(oldPointer),
            new IntPtr(Marshal.SizeOf(typeof(T)) * newElementCount))).ToPointer();
    }
}
