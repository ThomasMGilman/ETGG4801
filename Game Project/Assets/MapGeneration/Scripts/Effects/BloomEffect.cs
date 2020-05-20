using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class BloomEffect : MonoBehaviour
{
    [Range(1,16)]
    public int blurIterations = 1;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int width = source.width / 2;
        int height = source.height / 2;
        RenderTextureFormat format = source.format;
        RenderTexture[] textures = new RenderTexture[16];

        //Create Temporary RenderTexture
        RenderTexture currentTexDest = textures[0] = RenderTexture.GetTemporary(
            width,       //Width
            height,      //Height
            0,           //Depth Buffer
            format       //Texture Format
        );


        Graphics.Blit(source, currentTexDest);
        RenderTexture currentTexSource = currentTexDest;

        //Down sample
        int i = 1;
        for (; i < blurIterations; i++)
        {
            width /= 2;
            height /= 2;
            if (height < 2) break;  //stop blurring if pixel height less than 2
            currentTexDest = textures[i] = RenderTexture.GetTemporary(
                width,       //Width
                height,      //Height
                0,           //Depth Buffer
                format       //Texture Format
            );
            Graphics.Blit(currentTexSource, currentTexDest);
            currentTexSource = currentTexDest;
        }
        //Up sample
        for(i -= 2; i >= 0; i--)
        {
            currentTexDest = textures[i];
            textures[i] = null;
            Graphics.Blit(currentTexSource, currentTexDest);
            RenderTexture.ReleaseTemporary(currentTexSource);
            currentTexSource = currentTexDest;
        }

        Graphics.Blit(currentTexSource, destination);
        RenderTexture.ReleaseTemporary(currentTexSource);
    }
}
