﻿// Copyright 2003-2022 by Autodesk, Inc.
// 
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
// 
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
// 
// Use, duplication, or disclosure by the U.S. Government is subject to
// restrictions set forth in FAR 52.227-19 (Commercial Computer
// Software - Restricted Rights) and DFAR 252.227-7013(c)(1)(ii)
// (Rights in Technical Data and Computer Software), as applicable.

using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using RevitLookup.ViewModels.Objects;

namespace RevitLookup.Core;

public static class Snooper
{
    public static IReadOnlyList<SnoopableObject> SnoopView()
    {
        return new SnoopableObject[] {new(RevitApi.ActiveView)};
    }

    public static IReadOnlyList<SnoopableObject> SnoopDocument()
    {
        return new SnoopableObject[] {new(RevitApi.Document)};
    }

    public static IReadOnlyList<SnoopableObject> SnoopApplication()
    {
        return new SnoopableObject[] {new(RevitApi.Application)};
    }

    public static IReadOnlyList<SnoopableObject> SnoopEdge()
    {
        var geometryObject = SelectObject(ObjectType.Edge);
        return new[] {geometryObject};
    }

    public static IReadOnlyList<SnoopableObject> SnoopFace()
    {
        var geometryObject = SelectObject(ObjectType.Face);
        return new[] {geometryObject};
    }

    public static IReadOnlyList<SnoopableObject> SnoopLinkedElement()
    {
        var geometryObject = SelectObject(ObjectType.LinkedElement);
        return new[] {geometryObject};
    }

    public static IReadOnlyList<SnoopableObject> SnoopSelection()
    {
        var selectedIds = RevitApi.UiApplication.ActiveUIDocument.Selection.GetElementIds();
        if (selectedIds.Count > 0)
            return RevitApi.Document
                .GetElements(selectedIds)
                .Select(element => new SnoopableObject(RevitApi.Document, element))
                .ToList();

        return RevitApi.Document
            .GetElements(RevitApi.ActiveView.Id)
            .Select(element => new SnoopableObject(RevitApi.Document, element))
            .ToList();
    }

    public static IReadOnlyList<SnoopableObject> SnoopDatabase()
    {
        var elementTypes = RevitApi.Document.GetElements().WhereElementIsElementType();
        var elementInstances = RevitApi.Document.GetElements().WhereElementIsNotElementType();
        return elementTypes
            .UnionWith(elementInstances)
            .Select(element => new SnoopableObject(RevitApi.Document, element))
            .ToList();
    }

    public static IReadOnlyList<SnoopableObject> SnoopDependentElements()
    {
        var selectedIds = RevitApi.UiDocument.Selection.GetElementIds();
        if (selectedIds.Count == 0) return Array.Empty<SnoopableObject>();

        var elements = new List<ElementId>();
        var selectedElements = RevitApi.Document.GetElements(selectedIds).WhereElementIsNotElementType();

        foreach (var selectedElement in selectedElements)
        {
            var dependentElements = selectedElement.GetDependentElements(null);
            foreach (var dependentElement in dependentElements) elements.Add(dependentElement);
        }

        return new FilteredElementCollector(RevitApi.Document, elements)
            .Select(element => new SnoopableObject(RevitApi.Document, element))
            .ToList();
    }

    private static SnoopableObject SelectObject(ObjectType objectType)
    {
        Reference reference = null;
        try
        {
            reference = RevitApi.UiDocument.Selection.PickObject(objectType);
        }
        catch
        {
            // ignored, canceled by user
        }

        if (reference is null) return new SnoopableObject(RevitApi.Document, RevitApi.ActiveView);

        object element;
        Document document;
        switch (objectType)
        {
            case ObjectType.Edge:
            case ObjectType.Face:
                document = RevitApi.Document;
                element = document.GetElement(reference).GetGeometryObjectFromReference(reference);
                break;
            case ObjectType.LinkedElement:
                var representation = reference.ConvertToStableRepresentation(RevitApi.Document).Split(':')[0];
                var parsedReference = Reference.ParseFromStableRepresentation(RevitApi.Document, representation);
                var revitLinkInstance = (RevitLinkInstance) RevitApi.Document.GetElement(parsedReference);
                document = revitLinkInstance.GetLinkDocument();
                element = document.GetElement(reference.LinkedElementId);
                break;
            case ObjectType.Nothing:
            case ObjectType.Element:
            case ObjectType.PointOnElement:
            case ObjectType.Subelement:
            default:
                throw new NotSupportedException();
        }

        return new SnoopableObject(document, element);
    }
}