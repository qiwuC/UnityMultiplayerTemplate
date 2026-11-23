using System.IO;using System.Text;using UnityEditor;using UnityEngine;
namespace Cqw.MultilayerFramework.Tools
{
    public static class InteractableWizard
    {
        [MenuItem("Tools/Multilayer/Wizards/New Interactable")]
        public static void Open()
        {
            var name = "StressTest";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(typeof(Cqw.MultilayerFramework.Interaction.InteractableData)), "Assets/ScriptableObjects/Interaction/" + name + "Data.asset");
            var dir = Path.Combine(Application.dataPath, "Scripts/Interaction/Samples");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("namespace Cqw.MultilayerFramework.Interaction.Samples");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {name} : InteractableBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine("        public override void OnInteract(GameObject interactor)");
            sb.AppendLine("        {");
            sb.AppendLine("            UnityEngine.Debug.Log($\"[{name}] interacted\");");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(dir, name + ".cs"), sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[InteractableWizard] {name}.cs + Data.asset 生成完毕。");
        }
    }
}
