
using System;
using System.Collections.Generic;
using UnityEngine;

public static class STDUtils {

    public static void Swap<T>(ref T l, ref T r) {
        var temp = l;
        l = r;
        r = temp;
    }

    public static KeyValuePair<Texture, Texture> SwapTextures(Texture l, Texture r)
    {
        var temp = l;
        Graphics.CopyTexture(r, l);
        Graphics.CopyTexture(temp, r);
        return new KeyValuePair<Texture, Texture>(l, r);
    }
    
    public static void Release(ref ComputeBuffer buffer)
    {
        if (buffer == null) return;
        buffer.Release();
        buffer = null;
    }
    
    public static void Swap(ComputeBuffer[] buffers)
    {
        ComputeBuffer tmp = buffers[0];
        buffers[0] = buffers[1];
        buffers[1] = tmp;
    }
    
    public static void Release(IList<ComputeBuffer> buffers)
    {
        if (buffers == null) return;

        int count = buffers.Count;
        for (int i = 0; i < count; i++)
        {
            if (buffers[i] == null) continue;
            buffers[i].Release();
            buffers[i] = null;
        }
    }
}
