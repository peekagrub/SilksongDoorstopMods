using System;
using Mono.Cecil;
using UnityEngine;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class OnGUIPatch : Patch
{
    public OnGUIPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule)
        : base(targetModule, sourceModule, "GameManager", "OnGUI") { }

    override public void ApplyPatch()
    {
        ILProcessor.Clear();
        CopySourceMethod();
    }
}

internal class GameManager : global::GameManager
{
    private void OnGUI()
    {
        if (GetSceneNameString() == Constants.MENU_SCENE)
        {
            var oldBackgroundColor = GUI.backgroundColor;
            var oldContentColor = GUI.contentColor;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f)
            );

            string WarningText = "Doorstop Patches";

            GUI.Label(
                new Rect(20f, 20f, 200f, 200f),
                WarningText,
                new GUIStyle
                {
                    fontSize = 30,
                    normal = new GUIStyleState
                    {
                        textColor = Color.white,
                    }
                }
            );

            GUI.backgroundColor = oldBackgroundColor;
            GUI.contentColor = oldContentColor;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
    }
}
