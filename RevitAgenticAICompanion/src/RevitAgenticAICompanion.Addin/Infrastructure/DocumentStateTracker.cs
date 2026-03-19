using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;

namespace RevitAgenticAICompanion.Infrastructure
{
    public sealed class DocumentStateTracker
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, int> _revisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private UIControlledApplication _application;

        public void Attach(UIControlledApplication application)
        {
            if (_application != null)
            {
                return;
            }

            _application = application;
            _application.ControlledApplication.DocumentChanged += OnDocumentChanged;
        }

        public void Detach(UIControlledApplication application)
        {
            if (_application == null)
            {
                return;
            }

            _application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            _application = null;
        }

        public DocumentFingerprint Capture(Document document)
        {
            if (document == null)
            {
                return new DocumentFingerprint("no-document", 0);
            }

            var documentKey = GetDocumentKey(document);
            lock (_sync)
            {
                if (!_revisions.ContainsKey(documentKey))
                {
                    _revisions[documentKey] = 0;
                }

                return new DocumentFingerprint(documentKey, _revisions[documentKey]);
            }
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            var document = args.GetDocument();
            if (document == null)
            {
                return;
            }

            var documentKey = GetDocumentKey(document);
            lock (_sync)
            {
                if (_revisions.ContainsKey(documentKey))
                {
                    _revisions[documentKey] += 1;
                }
                else
                {
                    _revisions[documentKey] = 1;
                }
            }
        }

        private static string GetDocumentKey(Document document)
        {
            var path = document.PathName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return "unsaved:" + document.Title;
        }
    }
}
