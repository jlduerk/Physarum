using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public struct DispatchDimensions
{
    public uint dispatch_x;
    public uint dispatch_y;
    public uint dispatch_z;
}

public static class ComputeShaderUtility
{

    // ------------------------------------------------------------------
    // VARIABLES
    public static Dictionary<ComputeShader, Dictionary<int, string>> kernelsToNames; //maintains all kernels and associated names

    // ------------------------------------------------------------------
    // INITALISATION
    public static void Initialize()
    {
        if (kernelsToNames != null) kernelsToNames.Clear();
        else kernelsToNames = new Dictionary<ComputeShader, Dictionary<int, string>>();
    }

    // ------------------------------------------------------------------
    // DESTRUCTOR
    public static void Release()
    {
        kernelsToNames.Clear();
    }

    public static DispatchDimensions CheckGetDispatchDimensions(ComputeShader shader, int handle, uint desired_threadNum_x, uint desired_threadNum_y, uint desired_threadNum_z)
    {

        uint group_size_x, group_size_y, group_size_z;

        shader.GetKernelThreadGroupSizes(handle, out group_size_x, out group_size_y, out group_size_z);
        string kernelName = GetKernelNameFromHandle(shader, handle);


        if (desired_threadNum_x % group_size_x != 0 || desired_threadNum_y % group_size_y != 0 || desired_threadNum_z % group_size_z != 0)
        {

            Debug.LogError(string.Format("MISMATCHED THREAD NUMBERS AND GROUP NUMBERS."));
            Debug.Break();
        }


        DispatchDimensions dp;

        dp.dispatch_x = desired_threadNum_x / group_size_x;
        dp.dispatch_y = desired_threadNum_y / group_size_y;
        dp.dispatch_z = desired_threadNum_z / group_size_z;

        uint totalThreadNumber = desired_threadNum_x * desired_threadNum_y * desired_threadNum_z;

        return dp;
    }



    public static string GetKernelNameFromHandle(ComputeShader cp, int handle)
    {
        if (kernelsToNames.ContainsKey(cp))
        {
            if (kernelsToNames[cp].ContainsKey(handle))
                return kernelsToNames[cp][handle];
        }
        return handle.ToString();
    }

    public static int GetKernelHandle(ComputeShader cp, string name)
    {
        int handle = cp.FindKernel(name);

        Dictionary<int, string> cp_kernles;

        if (kernelsToNames.ContainsKey(cp)) cp_kernles = kernelsToNames[cp];
        else
        {
            cp_kernles = new Dictionary<int, string>();
            kernelsToNames.Add(cp, cp_kernles);       // Add this dictionary to the other when you create it. Since this is a reference type, you dont need to add this if you have already done it

        }
        cp_kernles.Add(handle, name);


        return handle;
    }
}




