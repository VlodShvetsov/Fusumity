using Fusumity.Attributes.Specific;
using UnityEditor;

namespace Fusumity.Editor.Drawers.Specific
{
	[CustomPropertyDrawer(typeof(RemoveFoldoutAttribute))]
	public class RemoveFoldoutDrawer : GenericPropertyDrawer
	{
		public override void ModifyPropertyData()
		{
			base.ModifyPropertyData();

			propertyData.property.isExpanded = true;
			propertyData.hasFoldout = false;
		}
	}
}
