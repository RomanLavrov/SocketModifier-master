#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace SocketModifier
{
    public class TargetWalls
    {
        public Element element { get; set; }
        public string material { get; set; }
        public BoundingBoxXYZ box { get; set; }
    }

    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
           
            ChangeParameters(app, doc);

            return Result.Succeeded;
        }

        private void DisplayWalls(List<TargetWalls> list)
        {
            string temp = string.Empty;
            foreach (TargetWalls wall in list)
            {
                temp += wall.element.Name + " - " + wall.material +  "\n";
            }
            TaskDialog.Show("Walls", temp);
        }

        private BoundingBoxIntersectsFilter Filter(Element wall, Document doc)
        {
            BoundingBoxXYZ box = wall.get_BoundingBox(null);
            if (box != null)
            {
               // TaskDialog.Show("BoundingBox", box.Min.ToString());
                Outline outline = new Outline(box.Min, box.Max);
                BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);
                return filter;
            }
            return null;
        }

        private List<FamilyInstance> GetCategoryDevices(Document doc, Element wall, BuiltInCategory category)
        {
            BoundingBoxIntersectsFilter filter = Filter(wall, doc);
            List<FamilyInstance> list = new List<FamilyInstance>();
            if (filter != null)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(category)
                    .WherePasses(filter);
                
                foreach (FamilyInstance instance in collector)
                {
                    list.Add(instance);
                    
                }
            }
            return list;
        }

        private List<FamilyInstance> GetDevices(Document doc, Element wall)
        {
            List<FamilyInstance> deviceList = new List<FamilyInstance>();

            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_ElectricalFixtures));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_ElectricalEquipment));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_LightingDevices));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_DataDevices));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_TelephoneDevices));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_FireAlarmDeviceTags));
            deviceList.AddRange(GetCategoryDevices(doc, wall, BuiltInCategory.OST_CommunicationDevices));

            return deviceList;
        }

        private void AddParameterData(Document doc, List<FamilyInstance> devices, TargetWalls wall)
        {
            using (Transaction trans = new Transaction(doc, "Adding Parameter"))
            {
                trans.Start();

                foreach (FamilyInstance instance in devices)
                {
                    Parameter material = instance.LookupParameter("WandTyp");
                    material.Set(wall.material);
                }
                trans.Commit();
            }
        }

        private List<Document> GetLinkedDocuments(Application app)
        {
            List<Document> list = new List<Document>();
            foreach (Document doc in app.Documents)
            {
                if (doc.IsLinked)
                {
                    list.Add(doc);
                }
            }
            return list;
        }

        private List<TargetWalls> GetAllWalls(List<Document> documents)
        {
            List<TargetWalls> list = new List<TargetWalls>();
            foreach (Document doc in documents)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls);
                foreach (Element element in collector)
                {
                    TargetWalls tWall = new TargetWalls();
                    tWall.element = element;
                    foreach (Parameter param in element.Parameters)
                    {
                        if (param.Definition.Name.Contains("IfcMaterial"))
                        {
                            tWall.material = param.AsString();
                        }
                    }
                    list.Add(tWall);
                }
            }
            return list;
        }

        private void ChangeParameters(Application app, Document doc)
        {
            List<TargetWalls> walls = GetAllWalls(GetLinkedDocuments(app));
            foreach (TargetWalls wall in walls)
            {
                List<FamilyInstance> wallDevices = GetDevices(doc, wall.element);
                AddParameterData(doc, wallDevices, wall);
            }
        }
    }
}
