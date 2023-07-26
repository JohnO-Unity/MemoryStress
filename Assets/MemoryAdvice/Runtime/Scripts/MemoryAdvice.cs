/*
 * Copyright 2022 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     https://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using AOT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class MemoryAdvice
{
    #region unity_memoryadvice
    [DllImport("unity_memoryadvice", CharSet = CharSet.Unicode)]
    private static extern int Unity_MemoryAdvice_init();

    [DllImport("unity_memoryadvice", CharSet = CharSet.Unicode)]
    private static extern int Unity_MemoryAdvice_initWithParams(String initParams);
    #endregion

    private delegate void MemoryAdviceWatcherDelegateCallbackForC(int memoryState, IntPtr user_data);

    #region memory_advice
    [DllImport("memory_advice", CharSet = CharSet.Unicode)]
    private static extern float MemoryAdvice_getPercentageAvailableMemory();

    [DllImport("memory_advice", CharSet = CharSet.Unicode)]
    private static extern int MemoryAdvice_getMemoryState();

    [DllImport("memory_advice", CharSet = CharSet.Unicode)]
    private static extern int MemoryAdvice_registerWatcher(ulong interval,
        MemoryAdviceWatcherDelegateCallbackForC callback, IntPtr user_data);
    #endregion

    private static readonly LinkedList<MemoryWatcherDelegateListener> memoryWatcherListeners = new LinkedList<MemoryWatcherDelegateListener>();

    public static MemoryAdviceErrorCode Init()
    {
        int CErrorCode = Unity_MemoryAdvice_init();
        Debug.Log("Init Memory Advice result code: " + CErrorCode);
        return (MemoryAdviceErrorCode)CErrorCode;
    }

    public static MemoryState GetMemoryState()
    {
        return (MemoryState)MemoryAdvice_getMemoryState();
    }

    public static MemoryAdviceErrorCode RegisterWatcher(ulong interval, MemoryWatcherDelegateListener callback)
    {
        memoryWatcherListeners.AddLast(callback);
        //call register without any user data
        return (MemoryAdviceErrorCode)MemoryAdvice_registerWatcher(interval, MemoryWatcherDefaultListener, new IntPtr());
    }

    public static MemoryAdviceErrorCode UnregisterWatcher(MemoryWatcherDelegateListener callback)
    {
        if (memoryWatcherListeners.Remove(callback))
        {
            return MemoryAdviceErrorCode.Ok;
        }
        else
        {
            return MemoryAdviceErrorCode.WatcherNotFound;
        }
    }

    [MonoPInvokeCallback(typeof(MemoryAdviceWatcherDelegateCallbackForC))]
    private static void MemoryWatcherDefaultListener(int memoryState, IntPtr user_data)
    {
        Debug.Log("MemoryWatcherCallback state: " + memoryState);
        for (int i = 0; i < memoryWatcherListeners.Count; i++)
        {
            memoryWatcherListeners.ElementAt(i)((MemoryState)memoryState);
        }
    }
}

public enum MemoryState
{
    Unknown = 0,  //< The memory state cannot be determined.
    OK = 1,  //< The application can safely allocate memory.
    ApproachingLimit = 2,  //< The application should minimize memory allocation.
    Critical =
        3,  //< The application should free memory as soon as possible, until
            //< the memory state changes.
}

public enum MemoryAdviceErrorCode
{
    Ok = 0,  //< No error
    NotInitialized = -1, //< A call was made before MemoryAdvice was initialized.
    AlreadyInitialized = -2, //< MemoryAdvice_init was called more than once.
    LookUpTableInvalid = -3, //< The provided lookup table was not a valid json object.
    AdvisorParameterInvalid = -4, //< The provided advisor parameters was not a valid json object.
    WatcherNotFound = -5, //< UnregisterWatcher was called with an invalid callback.
    TFLiteModelInvalid = -6 //< A correct TFLite model was not provided.
}

public delegate void MemoryWatcherDelegateListener(MemoryState state);