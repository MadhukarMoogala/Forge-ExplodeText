
using AcRx = Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Runtime.InteropServices;
using System.IO;

[assembly: AcRx.CommandClass(typeof(Forge.Main.Commands))]

namespace Forge.Main
{
    public static class Point3dExtensions
    {
        public static Point2d ConvertToPoint2D(this Point3d p)
        {
            return new Point2d(p.X, p.Y);
        }


    }
    public static class EditorExtensions
    {
        //The UCS to WCS transformation matrix.</returns>
        public static Matrix3d UCS2WCS(this Editor ed)
        {
            return ed.CurrentUserCoordinateSystem;
        }
        //The WCS to UCS transformation matrix.</returns>
        public static Matrix3d WCS2UCS(this Editor ed)
        {
            return ed.CurrentUserCoordinateSystem.Inverse();
        }
        //The DCS to WCS transformation matrix.</returns>
        public static Matrix3d DCS2WCS(this Editor ed)
        {
            Matrix3d retVal = new Matrix3d();
            bool tilemode = ed.Document.Database.TileMode;
            if (!tilemode)
                ed.SwitchToModelSpace();
            using (ViewTableRecord vtr = ed.GetCurrentView())
            {
                retVal =
                    Matrix3d.Rotation(-vtr.ViewTwist, vtr.ViewDirection, vtr.Target) *
                    Matrix3d.Displacement(vtr.Target - Point3d.Origin) *
                    Matrix3d.PlaneToWorld(vtr.ViewDirection);
            }
            if (!tilemode)
                ed.SwitchToPaperSpace();
            return retVal;
        }
        //The WCS to DCS transformation matrix.</returns>
        public static Matrix3d WCS2DCS(this Editor ed)
        {
            return ed.DCS2WCS().Inverse();
        }

        //Gets the ObjectId of (entlast)
        static public ObjectId SelectLastEnt(this Editor ed)
        {
            PromptSelectionResult LastEnt = ed.SelectLast();
            if (LastEnt.Value != null && LastEnt.Value.Count == 1)
            {
                return LastEnt.Value[0].ObjectId;
            }
            return ObjectId.Null;
        }
        static public ObjectId[] SelectTextEntitesInModelSpace(this Editor ed)
        {
            TypedValue[] vals = new TypedValue[]
            {
                    new TypedValue((int)DxfCode.Start,"TEXT"),
                    new TypedValue((int)DxfCode.LayoutName,"MODEL"),
            };
            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(vals));
            if (res.Status == PromptStatus.OK)
            {
                return res.Value.GetObjectIds();
            }
            else
            {
                return null;
            }
        }
    }

    public class Commands
    {

        [AcRx.CommandMethod("FDACommands","EXPTXT",AcRx.CommandFlags.Modal)]
        public void EXPTXT()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // selection text
            /* var entOpts = new PromptStringOptions("\nHandle of text: ");
             entOpts.AllowSpaces = false;
             var entRes = ed.GetString(entOpts);
             if (entRes.Status != PromptStatus.OK)
                 return;
              ObjectId id = default(ObjectId);
             long Hl = Convert.ToInt64(entRes.StringResult, 16);
             Handle h = new Handle(Hl);
             id = db.GetObjectId(false, h, 0);  
            */

            ObjectId[] dbtextIds = ed.SelectTextEntitesInModelSpace();
            
           

           
           
            foreach (ObjectId id in dbtextIds)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var text = (DBText)tr.GetObject(id, OpenMode.ForWrite);
                    ObjectId[] ids = new ObjectId[1];
                    ids[0] = id;
                    ed.SetImpliedSelection(ids);
                    var tempFile = Path.Combine(Path.GetTempPath(), "Q.wmf");

                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                    ed.Command("_.WMFOUT", tempFile, "", "");
                    var viewSize = (double)Application.GetSystemVariable("VIEWSIZE");
                    var screenSize = (Point2d)Application.GetSystemVariable("SCREENSIZE");
                    double factor = viewSize * (screenSize.X / screenSize.Y);
                    var viewCtr = (Point3d)Application.GetSystemVariable("VIEWCTR");
                    //Transform viewCtr from UCS to DCS
                    Matrix3d matUCS2DCS = ed.UCS2WCS() * ed.WCS2DCS();
                    viewCtr = viewCtr.TransformBy(matUCS2DCS);
                    var p1 = new Point3d(viewCtr.X - (factor / 2.0), viewCtr.Y - (viewSize / 2.0), .0);
                    var p2 = new Point3d(viewCtr.X + (factor / 2.0), viewCtr.Y + (viewSize / 2.0), .0);
                    //Transorm p1,p2 from DCS to UCS;
                    Matrix3d matDCS2UCS = ed.DCS2WCS() * ed.WCS2UCS();
                    p1 = p1.TransformBy(matDCS2UCS);
                    p2 = p2.TransformBy(matDCS2UCS);
                    Point2d wmfinBlockPos = new Point2d(p1.X, p2.Y);
                    var tempWithOutExt = Path.Combine(Path.GetDirectoryName(tempFile), Path.GetFileNameWithoutExtension(tempFile));
                    ed.Command("_.WMFIN", tempWithOutExt, wmfinBlockPos, "2", "", "");
                    try
                    {
                        var wmfBlock = tr.GetObject(ed.SelectLastEnt(), OpenMode.ForWrite) as BlockReference;
                        DBObjectCollection pElems = new DBObjectCollection();
                        wmfBlock?.Explode(pElems);
                        var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        foreach (DBObject elem in pElems)
                        {
                            space.AppendEntity(elem as Entity);
                            tr.AddNewlyCreatedDBObject(elem, true);
                        }
                        //Purge unused WMFIN Block and reference
                        ObjectId wmfBtr = GetNonErasedTableRecordId(db.BlockTableId, wmfBlock.Name);
                        ObjectIdCollection blockIds = new ObjectIdCollection();
                        blockIds.Add(wmfBtr);
                        db.Purge(blockIds);
                        foreach (ObjectId oId in blockIds)
                        {
                            DBObject obj = tr.GetObject(oId, OpenMode.ForWrite);
                            obj.Erase();

                        }
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage(ex.Message);
                    }
                    finally
                    {
                        //Erase text entity
                        text.Erase();
                        tr.Commit();
                    }
                }

            }

        }

        public static  ObjectId[] SelectEntities(Editor ed)
        {
            TypedValue[] vals = new TypedValue[]
            {
                    new TypedValue((int)DxfCode.Start,"TEXT"),
                    new TypedValue((int)DxfCode.LayoutName,"MODEL"),
            };
            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(vals));
            if (res.Status == PromptStatus.OK)
            {
                return res.Value.GetObjectIds();
            }
            else
            {
                return null;
            }
        }
        //This the ultimate test to GetNonErasedTableRecords
        public static ObjectId GetNonErasedTableRecordId(ObjectId TableId, string Name)
        {
            ObjectId id = ObjectId.Null;
            using (Transaction tr = TableId.Database.TransactionManager.StartTransaction())
            {
                SymbolTable table = (SymbolTable)tr.GetObject(TableId, OpenMode.ForRead);
                if (table.Has(Name))
                {
                    id = table[Name];
                    if (!id.IsErased)
                        return id;
                    foreach (ObjectId recId in table)
                    {
                        if (!recId.IsErased)
                        {
                            SymbolTableRecord rec = (SymbolTableRecord)tr.GetObject(recId, OpenMode.ForRead);
                            if (string.Compare(rec.Name, Name, true) == 0)
                                return recId;
                        }
                    }
                }
            }
            return id;
        }
    }
    






        

    
}



