using Fusumity.Attributes.Specific;
using UnityEditor;
using UnityEngine;

namespace Fusumity.Editor.Drawers.Specific
{
	[CustomPropertyDrawer(typeof(AngleToRadAttribute))]
	public class AngleToRadDrawer : FusumityPropertyDrawer
	{
		public override void ModifyPropertyData()
		{
			currentPropertyData.label.text = currentPropertyData.label.text.Replace("Rad", "Angle") + "°";
			base.ModifyPropertyData();
		}

		public override void DrawSubBody(Rect position)
		{
			var rad = currentPropertyData.property.floatValue;
			EditorGUI.BeginChangeCheck();
			var angle = EditorGUI.FloatField(position, " ", rad * Mathf.Rad2Deg);
			if (angle == 360f)
				rad = Mathf.PI * 2;
			else
				rad = angle * Mathf.Deg2Rad;
			if (EditorGUI.EndChangeCheck())
				currentPropertyData.property.floatValue = rad;
		}
	}
}