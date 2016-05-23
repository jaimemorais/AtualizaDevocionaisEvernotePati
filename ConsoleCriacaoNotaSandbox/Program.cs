using Evernote.EDAM.NoteStore;
using Evernote.EDAM.Type;
using Evernote.EDAM.UserStore;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Thrift.Protocol;
using Thrift.Transport;

namespace AtualizaDevocionaisEvernotePat
{

    class Program
    {
        static void Main(string[] args)
        {

            // Get the contents of the site
            string pageURL = "http://aguasvivas.ws/";
            string pageStringContent = GetPageStringContent(pageURL);

            string token = ConfigurationManager.AppSettings["token"];
            
            string title = "Devocional - " + DateTime.Now.ToString("dd/MM/yyyy");

            // Create note in the production evernote
            CreateNote(token, title, pageStringContent);


            // Create note in the sandbox (dev) evernote environment
            //CreateNoteSandbox(token, pageStringContent);
        }


        private static void CreateNote(string authToken, string noteTitle, string noteContent)
        {            
            String evernoteHost = "www.evernote.com"; 

            Uri userStoreUrl = new Uri("https://" + evernoteHost + "/edam/user");
            TTransport userStoreTransport = new THttpClient(userStoreUrl);
            TProtocol userStoreProtocol = new TBinaryProtocol(userStoreTransport);
            UserStore.Client userStore = new UserStore.Client(userStoreProtocol);

            // Check API version
            bool versionOK =
                userStore.checkVersion("Evernote EDAMTest (C#)",
                   Evernote.EDAM.UserStore.Constants.EDAM_VERSION_MAJOR,
                   Evernote.EDAM.UserStore.Constants.EDAM_VERSION_MINOR);
            Console.WriteLine("Is my Evernote API version up to date? " + versionOK);
            if (!versionOK)
            {
                throw new Exception("API version not OK");                
            }

            
            String noteStoreUrl = userStore.getNoteStoreUrl(authToken);

            TTransport noteStoreTransport = new THttpClient(new Uri(noteStoreUrl));
            TProtocol noteStoreProtocol = new TBinaryProtocol(noteStoreTransport);
            NoteStore.Client noteStore = new NoteStore.Client(noteStoreProtocol);



            // List notebooks (cadernos do usuario)
            //List<Notebook> notebooks = noteStore.listNotebooks(authToken);

            // List linked notebook (cadernos compartilhados)
            List<LinkedNotebook> linkedNotebooks = noteStore.listLinkedNotebooks(authToken);
            LinkedNotebook devocionaisNotebook = linkedNotebooks.First(ln => ln.ShareName.Equals("DEVOCIONAIS"));
            
            
            // Create the note
            Note note = new Note();
            note.Title = noteTitle;
            //note.NotebookGuid = devocionaisNotebook.Guid;
            

            // The content of an Evernote note is represented using Evernote Markup Language
            // (ENML). The full ENML specification can be found in the Evernote API Overview
            // at http://dev.evernote.com/documentation/cloud/chapters/ENML.php
            note.Content =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\">" +
                "<en-note>" +
                noteContent +
                "</en-note>";
            
            // Finally, send the new note to Evernote using the createNote method
            // The new Note object that is returned will contain server-generated
            // attributes such as the new note's unique GUID.
            try
            {
                Note createdNote = noteStore.createNote(authToken, note);
                Console.WriteLine("Successfully created new note with GUID: " + createdNote.Guid);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error creating note : " + e.Message);
            }           

            
        }


        private static string GetPageStringContent(string pageURL)
        {
            string pageStringContent;
            using (WebClient wc = new WebClient())
            {
                pageStringContent = wc.DownloadString(pageURL);

                int startIndex = pageStringContent.IndexOf("<!-- insert the page content here -->");
                pageStringContent = pageStringContent.Substring(startIndex);

                HtmlAgilityPack.HtmlDocument h = new HtmlAgilityPack.HtmlDocument();
                h.LoadHtml(pageStringContent);

                using (StringWriter sw = new StringWriter())
                {
                    ConvertTo(h.DocumentNode, sw);
                    sw.Flush();
                    pageStringContent = sw.ToString();
                }
            }

            return pageStringContent;
        }
        
        private static void ConvertContentTo(HtmlNode node, TextWriter outText)
        {
            foreach (HtmlNode subnode in node.ChildNodes)
            {
                ConvertTo(subnode, outText);
            }
        }

        private static void ConvertTo(HtmlNode node, TextWriter outText)
        {
            string html;
            switch (node.NodeType)
            {
                case HtmlNodeType.Comment:
                    // don't output comments
                    break;

                case HtmlNodeType.Document:
                    ConvertContentTo(node, outText);
                    break;

                case HtmlNodeType.Text:
                    // script and style must not be output
                    string parentName = node.ParentNode.Name;
                    if ((parentName == "script") || (parentName == "style"))
                        break;

                    // get text
                    html = ((HtmlTextNode)node).Text;

                    // is it in fact a special closing node output as text?
                    if (HtmlNode.IsOverlappedClosingElement(html))
                        break;

                    // check the text is meaningful and not a bunch of whitespaces
                    if (html.Trim().Length > 0)
                    {
                        outText.Write(HtmlEntity.DeEntitize(html));
                    }
                    break;

                case HtmlNodeType.Element:
                    switch (node.Name)
                    {
                        case "p":                            
                            outText.Write("<br/><br/>");
                            break;
                    }

                    if (node.HasChildNodes)
                    {
                        ConvertContentTo(node, outText);
                    }
                    break;
            }
        }


        private static void CreateNoteSandbox(string developerToken, string noteContent)
        {
            // Real applications authenticate with Evernote using OAuth, but for the
            // purpose of exploring the API, you can get a developer token that allows
            // you to access your own Evernote account. To get a developer token, visit 
            // https://sandbox.evernote.com/api/DeveloperToken.action
            String authToken = developerToken;


            // Initial development is performed on our sandbox server. To use the production 
            // service, change "sandbox.evernote.com" to "www.evernote.com" and replace your
            // developer token above with a token from 
            // https://www.evernote.com/api/DeveloperToken.action
            String evernoteHost = "sandbox.evernote.com";

            Uri userStoreUrl = new Uri("https://" + evernoteHost + "/edam/user");
            TTransport userStoreTransport = new THttpClient(userStoreUrl);
            TProtocol userStoreProtocol = new TBinaryProtocol(userStoreTransport);
            UserStore.Client userStore = new UserStore.Client(userStoreProtocol);

            bool versionOK =
                userStore.checkVersion("Evernote EDAMTest (C#)",
                   Evernote.EDAM.UserStore.Constants.EDAM_VERSION_MAJOR,
                   Evernote.EDAM.UserStore.Constants.EDAM_VERSION_MINOR);
            Console.WriteLine("Is my Evernote API version up to date? " + versionOK);
            if (!versionOK)
            {
                return;
            }

            // Get the URL used to interact with the contents of the user's account
            // When your application authenticates using OAuth, the NoteStore URL will
            // be returned along with the auth token in the final OAuth request.
            // In that case, you don't need to make this call.
            String noteStoreUrl = userStore.getNoteStoreUrl(authToken);

            TTransport noteStoreTransport = new THttpClient(new Uri(noteStoreUrl));
            TProtocol noteStoreProtocol = new TBinaryProtocol(noteStoreTransport);
            NoteStore.Client noteStore = new NoteStore.Client(noteStoreProtocol);

            // List all of the notebooks in the user's account        
            List<Notebook> notebooks = noteStore.listNotebooks(authToken);
            Console.WriteLine("Found " + notebooks.Count + " notebooks:");
            foreach (Notebook notebook in notebooks)
            {
                Console.WriteLine("  * " + notebook.Name);
            }

            Console.WriteLine();
            Console.WriteLine("Creating a note in the default notebook");
            Console.WriteLine();

            // To create a new note, simply create a new Note object and fill in 
            // attributes such as the note's title.
            Note note = new Note();
            note.Title = "Test note " + DateTime.Now.ToString("dd/MM/yyyy");

            // The content of an Evernote note is represented using Evernote Markup Language
            // (ENML). The full ENML specification can be found in the Evernote API Overview
            // at http://dev.evernote.com/documentation/cloud/chapters/ENML.php
            note.Content = 
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\">" +
                "<en-note>" +
                noteContent +
                "</en-note>";

            // Finally, send the new note to Evernote using the createNote method
            // The new Note object that is returned will contain server-generated
            // attributes such as the new note's unique GUID.
            Note createdNote = noteStore.createNote(authToken, note);

            Console.WriteLine("Successfully created new note with GUID: " + createdNote.Guid);
        }
    }
}
