using System;

namespace Fusumity.Attributes.Specific
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
	public class ButtonAttribute : GenericDrawerAttribute
	{
		public string buttonName;
		public string methodPath;
		// Hide label, body and subBody
		public bool hidePropertyField;
		public bool drawBefore;

		public ButtonAttribute(string buttonName, string methodPath, bool hidePropertyField = false, bool drawBefore = true)
		{
			this.buttonName = buttonName;
			this.methodPath = methodPath;
			this.hidePropertyField = hidePropertyField;
			this.drawBefore = drawBefore;
		}

		public ButtonAttribute(string methodPath, bool hidePropertyField = false, bool drawBefore = true)
		{
			this.buttonName = null;
			this.methodPath = methodPath;
			this.hidePropertyField = hidePropertyField;
			this.drawBefore = drawBefore;
		}
	}
}
