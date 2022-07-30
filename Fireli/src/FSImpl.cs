using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Web;
using System.Configuration;
using System.Threading;

namespace Fireli.FSImpl {

    #region .dirinfo.xml management
    class XmlFileInfo {
        protected Guid guid;
        public Guid Guid {
            get { return guid; }
            set { guid = value; }
        }

        protected string name;
        public string Name {
            get { return name; }
            set { name = value; }
        }

        protected string originalName;
        public string OriginalName {
            get { return originalName; }
            set { originalName = value; }
        }

        internal XmlFileInfo(XmlElement x) {
            this.guid = new Guid(x.Attributes["guid"].Value);
            this.name = x.Attributes["vpath"].Value;
            this.originalName = x.Attributes[x.HasAttribute("originalFilename") ? "originalFilename" : "vpath"].Value;
        }

        internal XmlFileInfo(Guid guid, string name, string originalName) {
            this.guid = guid;
            this.name = name;
            this.originalName = originalName;
        }

        internal void CopyTo(XmlElement x) {
            x.SetAttribute("guid", guid.ToString());
            x.SetAttribute("vpath", name);
            x.SetAttribute("originalFilename", originalName);
        }
    }

    class XmlDirInfo {
        protected string filename;
        protected XmlDocument doc;

        internal XmlDirInfo(string filename) {
            this.filename = filename;

            doc = new XmlDocument();
            if (File.Exists(filename))
                Retry(() => doc.Load(filename));
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "root") {
                doc = new XmlDocument();
                root = doc.CreateElement("root");
                doc.AppendChild(root);
            }
        }

        internal bool QuotasCustomSettings {
            get {
                if (doc != null && doc.DocumentElement != null && doc.DocumentElement.Attributes["quotas-custom-settings"] != null && !string.IsNullOrEmpty(doc.DocumentElement.Attributes["quotas-custom-settings"].Value)) {
                    try {
                        return Convert.ToBoolean(doc.DocumentElement.Attributes["quotas-custom-settings"].Value);
                    } catch (FormatException) {
                        return false;
                    }
                } else
                    return false;
            }
            set {
                doc.DocumentElement.SetAttribute("quotas-custom-settings", value.ToString());
                doc.Save(filename);
            }
        }

        internal bool QuotasEnabled {
            get {
                if (doc != null && doc.DocumentElement != null && doc.DocumentElement.Attributes["quotas-enabled"] != null && !string.IsNullOrEmpty(doc.DocumentElement.Attributes["quotas-enabled"].Value)) {
                    try {
                        return Convert.ToBoolean(doc.DocumentElement.Attributes["quotas-enabled"].Value);
                    } catch (FormatException) {
                        return false;
                    }
                } else
                    return false;
            }
            set {
                doc.DocumentElement.SetAttribute("quotas-enabled", value.ToString());
                doc.Save(filename);
            }
        }

        internal long QuotasLimit {
            get {
                if (doc != null && doc.DocumentElement != null && doc.DocumentElement.Attributes["quotas-limit"] != null && !string.IsNullOrEmpty(doc.DocumentElement.Attributes["quotas-limit"].Value)) {
                    try {
                        return Convert.ToInt64(doc.DocumentElement.Attributes["quotas-limit"].Value);
                    } catch (FormatException) {
                        return 0L;
                    }
                } else
                    return 0L;
            }
            set {
                doc.DocumentElement.SetAttribute("quotas-limit", value.ToString());
                doc.Save(filename);
            }
        }

        internal void AddFile(XmlFileInfo fileInfo) {
            XmlElement root = doc.DocumentElement;
            XmlElement file = doc.CreateElement("file");
            fileInfo.CopyTo(file);
            root.AppendChild(file);
            Retry(() => doc.Save(filename));
        }

        internal void AddFile(Guid guid, string vpath) {
            AddFile(new XmlFileInfo(guid, vpath, vpath));
        }

        internal void AssertCanRename(Guid guid, string NewName) {
            XmlElement x = GetFileXmlElement(NewName);
            if (x != null)
                throw new Exception("File '" + NewName + "' already exists.");

            x = GetFileXmlElement(guid);
            if (x == null)
                throw new Exception("File '" + guid + "' not found");
        }

        internal void Rename(Guid guid, string NewName) {
            AssertCanRename(guid, NewName);
            XmlElement x = GetFileXmlElement(guid);
            x.Attributes["vpath"].Value = NewName;
            x.Attributes["originalFilename"].Value = NewName;
            doc.Save(filename);
        }

        private XmlElement GetFileXmlElement(Guid guid) {
            foreach (XmlNode n in doc.DocumentElement.ChildNodes)
                if (((XmlElement)n).Attributes["guid"].Value == guid.ToString())
                    return (XmlElement)n;
            return null;
        }

        private XmlElement GetFileXmlElement(string vpath) {
            foreach (XmlNode n in doc.DocumentElement.ChildNodes)
                if (((XmlElement)n).Attributes["vpath"].Value == vpath)
                    return (XmlElement)n;
            return null;
        }

        internal XmlFileInfo GetFile(Guid guid) {
            XmlElement e = GetFileXmlElement(guid);
            return e != null ? new XmlFileInfo(e) : null;
        }

        internal XmlFileInfo GetFile(string vpath) {
            XmlElement e = GetFileXmlElement(vpath);
            return e != null ? new XmlFileInfo(e) : null;
        }

        internal IList<XmlFileInfo> GetAll() {
            List<XmlFileInfo> result = new List<XmlFileInfo>();
            foreach (XmlNode xmln in doc.DocumentElement.ChildNodes) {
                XmlElement xmle = (XmlElement)xmln;
                result.Add(new XmlFileInfo(xmle));
            }
            return result;
        }

        internal void Delete(Guid guid) {
            XmlElement e = GetFileXmlElement(guid);
            if (e != null)
                doc.DocumentElement.RemoveChild(e);
            doc.Save(filename);
        }

        private void Retry(Action action)
        {
            for (var i = 0; ; i++)
                try
                {
                    action();
                    break;
                }
                catch (IOException)
                {
                    if (i >= 3)
                        throw;
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                }
        }
    }
    #endregion

    public class FSRepManConfigurator {
        public void Configure(FSRepMan fsrepman) {
            string strType = ConfigurationManager.AppSettings["Dac6FileRepositoryPathType"] ?? "local";
            string strFolder = ConfigurationManager.AppSettings["Dac6FileRepositoryPath"] ?? "~/attachments";
            if (strType == "local") {

                fsrepman.RootFolder = (!VirtualPathUtility.IsAppRelative(strFolder) ? strFolder : HttpContext.Current.Server.MapPath(strFolder));
            } else {
                fsrepman.RootFolder = HttpContext.Current.Server.MapPath(strFolder);
            }

            try {
                fsrepman.QuotasEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings["FSRepMan.QuotasEnabled"]);
            } catch (FormatException) {
                fsrepman.QuotasEnabled = false;
            }

            try {
                fsrepman.QuotasLimit = Convert.ToInt64(ConfigurationManager.AppSettings["FSRepMan.QuotasLimit"]);
            } catch (FormatException) {
                fsrepman.QuotasLimit = 0;
            }
        }

    }

    public class FSRepMan : IRepMan {
        private string rootFolder;

        public FSRepMan() {
            FSRepManConfigurator c = new FSRepManConfigurator();
            c.Configure(this);
        }

        #region IRepMan Members

        public IRepository Acquire(string name) {
            return new FSRepository(this, name);
        }

        public bool HasRepository(string name) {
            return Directory.Exists(Path.Combine(rootFolder, name));
        }

        private bool quotasEnabled;

        public bool QuotasEnabled {
            get { return quotasEnabled; }
            set { quotasEnabled = value; }
        }

        private long quotasLimit;

        public long QuotasLimit {
            get { return quotasLimit; }
            set { quotasLimit = value; }
        }


        #endregion

        /// <summary>
        /// The physical path of the root folder, the folder where all repositories folder are stored.
        /// </summary>
        public string RootFolder {
            get { return rootFolder; }
            internal set {
                rootFolder = value;
                Directory.CreateDirectory(rootFolder);
            }
        }
    }

    public class FSRepository : FolderImpl, IRepository {
        private FSRepMan owner;

        public FSRepository(FSRepMan owner, string name)
            : base(null, name, Guid.Empty, name) {
            this.owner = owner;
        }

        internal void EnsureRepositoryFolder() { Directory.CreateDirectory(RealPath); }

        public string MapPath(string VirtualPath) {
            if (VirtualPath.StartsWith("/"))
                VirtualPath = VirtualPath.Substring(1);
            VirtualPath = VirtualPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(RealPath, VirtualPath);
        }

        public override string RealPath {
            get {
                return Path.Combine(owner.RootFolder, this.name);
            }
        }
        #region IFileEntry Members
        public override void Delete(bool ignoreErrors) {
            try {
                Directory.Delete(RealPath, true);
            } catch (IOException ex) {
                if (!ignoreErrors)
                    throw ex;
            }
        }
        #endregion

        #region IFolder Members

        public override Guid AddFile(string Name, Stream inputStream, AddFileMode addFileMode) {
            EnsureRepositoryFolder();
            return base.AddFile(Name, inputStream, addFileMode);
        }


        #endregion

        #region IRepository Members
        public Guid Import(IFile file) {
            if (file == null)
                throw new ArgumentException("File cannot be null", "file");

            using (Stream inStream = file.GetInputStream()) {
                return AddFile(file.OriginalFilename, inStream, AddFileMode.ChangeName);
            }
        }

        public bool QuotasCustomSettings {
            get {
                return XmlDirInfo.QuotasCustomSettings;
            }
            set {
                XmlDirInfo.QuotasCustomSettings = value;
            }
        }

        public bool QuotasEnabled {
            get {
                return XmlDirInfo.QuotasEnabled;
            }
            set {
                XmlDirInfo.QuotasEnabled = value;
            }
        }

        public long QuotasLimit {
            get {
                return XmlDirInfo.QuotasLimit;
            }
            set {
                XmlDirInfo.QuotasLimit = value;
            }
        }

        public long GetEffectiveQuotasLimit() {
            if (QuotasCustomSettings)
                return QuotasEnabled ? QuotasLimit : 0L;
            else
                return owner.QuotasEnabled ? owner.QuotasLimit : 0L;
        }
        #endregion
    }

    public class FileEntryImpl : IFileEntry {
        protected IFolder parent;
        protected string name;
        protected Guid guid;
        protected string originalFilename;

        internal FileEntryImpl(IFolder parent, string name, Guid guid, string originalFilename) {
            this.parent = parent;
            this.name = name;
            this.guid = guid;
            this.originalFilename = originalFilename;
        }

        public IRepository Repository {
            get {
                if (this is FSRepository)
                    return (FSRepository)this;
                IFolder f = this.Parent;
                while (!(f is FSRepository))
                    f = ((FileEntryImpl)f).Parent;
                return (FSRepository)f;
            }
        }

        public virtual string RealPath {
            get {
                return ((FSRepository)Repository).MapPath(VirtualPath);
            }
        }

        #region IFileEntry Members

        public Guid Guid {
            get { return guid; }
        }

        public bool IsFolder {
            get { return Directory.Exists(RealPath); }
        }

        public long Size {
            get {
                if (IsFolder)
                    return 0L;
                try {
                    FileInfo fi = new FileInfo(RealPath);
                    return fi.Length;
                } catch (FileNotFoundException) {
                    return -1;
                }
            }
        }

        public string Name {
            get { return name; }
        }

        public string OriginalFilename {
            get { return originalFilename; }
        }

        public string FullPath {
            get {
                if (parent == null)
                    return name;
                return parent.FullPath + "/" + name;
            }
        }

        public string VirtualPath {
            get {
                if (parent == null) // this is a repository
                    return string.Empty;
                return parent.VirtualPath + "/" + name;
            }
        }

        public virtual void Delete(bool ignoreErrors) {
            parent.Delete(this, ignoreErrors);
        }

        public virtual void Rename(string NewName) {
            parent.Rename(this, NewName);
        }

        public IFolder Parent { get { return parent; } }
        #endregion
    }

    public class FileImpl : FileEntryImpl, IFile {
        internal FileImpl(IFolder parent, string name, Guid guid, string originalFilename)
            :
            base(parent, name, guid, originalFilename) {
        }

        #region IFile Members

        public virtual Stream GetInputStream() {
            return new FileStream(RealPath, FileMode.Open, FileAccess.Read);
        }

        #endregion
    }

    public class FolderImpl : FileEntryImpl, IFolder {
        internal FolderImpl(IFolder parent, string name, Guid guid, string originalFilename)
            :
            base(parent, name, guid, originalFilename) {
        }

        #region IFolder Members

        public virtual IList<IFile> GetFiles() {
            List<IFile> result = new List<IFile>();
            foreach (XmlFileInfo fi in XmlDirInfo.GetAll())
                result.Add(new FileImpl(this, fi.Name, fi.Guid, fi.OriginalName));
            return result;
        }

        public virtual IList<IFolder> GetFolders() {
            if (!Directory.Exists(this.RealPath))
                return new List<IFolder>();
            string[] arrPaths = Directory.GetDirectories(this.RealPath);
            List<IFolder> result = new List<IFolder>();
            foreach (string str in arrPaths) {
                string strName = Path.GetFileName(str);
                result.Add(new FolderImpl(this, strName, Guid.Empty, strName));
            }
            return result;
        }

        public virtual Guid AddFile(string Name, Stream inputStream, AddFileMode addFileMode) {
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException("Parameter cannot be null or empty", "Name");

            if (Name.Contains("/"))
                throw new ArgumentException("Name cannot contain slash character", "Name");

            long effLimit = Repository.GetEffectiveQuotasLimit();
            if (effLimit > 0 && Repository.GetTotalSize() > effLimit)
                throw new Exception("Quotas limit reached.");

            string vpath = Name;
            string rpath = Path.Combine(RealPath, Name);
            if (Directory.Exists(rpath))
                throw new Exception("A folder by that name already exists");

            bool fexists = File.Exists(rpath);
            if (fexists && addFileMode == AddFileMode.Throw)
                throw new Exception("A file by that name already exists");

            if (fexists && addFileMode == AddFileMode.ChangeName) {
                int i = 0;
                do {
                    vpath = Path.GetFileNameWithoutExtension(Name) + "-" + (++i) + Path.GetExtension(Name);
                    rpath = Path.Combine(RealPath, vpath);
                } while (File.Exists(rpath));
            }

            FileStream fs = new FileStream(rpath, FileMode.Create, FileAccess.ReadWrite);
            byte[] buff = new byte[8192];
            int btRead;
            do {
                btRead = inputStream.Read(buff, 0, 8192);
                if (btRead > 0)
                    fs.Write(buff, 0, btRead);
            } while (btRead > 0);
            fs.Close();

            XmlFileInfo xfi = XmlDirInfo.GetFile(vpath);
            if (xfi == null) {
                Guid result = Guid.NewGuid();
                XmlDirInfo.AddFile(new XmlFileInfo(result, vpath, Name));
                return result;
            } else
                return xfi.Guid;
        }

        public virtual void MkDir(string Name) {
            Directory.CreateDirectory(Path.Combine(RealPath, Name));
        }

        public virtual IFile GetBy(Guid guid, bool recursive) {
            XmlFileInfo fi = XmlDirInfo.GetFile(guid);
            if (fi != null)
                return new FileImpl(this, fi.Name, fi.Guid, fi.OriginalName);
            if (recursive) {
                IList<IFolder> lstFolders = GetFolders();
                foreach (IFolder folder in lstFolders) {
                    IFile f = folder.GetBy(guid, recursive);
                    if (f != null)
                        return f;
                }
            }
            return null;
        }


        public virtual IFileEntry GetBy(string VirtualPath) {
            if (string.IsNullOrEmpty(VirtualPath))
                return this;

            if (VirtualPath.StartsWith("/"))
                return Repository.GetBy(VirtualPath.Substring(1));

            string[] arrPath = VirtualPath.Split('/');
            FolderImpl current = this;
            for (int i = 0; i < arrPath.Length; i++) {
                string str = arrPath[i];
                if (string.IsNullOrEmpty(str))
                    throw new ArgumentException("Path was invalid because it contained empty path element", "VirtualPath");

                if (i < arrPath.Length - 1) {
                    // we're not at the final element so we're still looking for folders
                    if (!Directory.Exists(Path.Combine(current.RealPath, str)))
                        throw new Exception("Path " + Path.Combine(current.RealPath, str) + " does not exist");
                    current = new FolderImpl(current, str, Guid.Empty, str);
                } else {
                    // we're at the final element so we can find either a file or a folder
                    if (Directory.Exists(Path.Combine(current.RealPath, str)))
                        return new FolderImpl(current, str, Guid.Empty, str);

                    XmlFileInfo xfi = XmlDirInfo.GetFile(str);
                    if (xfi != null)
                        return new FileImpl(current, str, xfi.Guid, xfi.OriginalName);
                    else
                        return null;
                }
            }
            return this;
        }

        public void Delete(IFileEntry fe, bool ignoreErrors) {
            if (!(fe is FileEntryImpl))
                throw new ArgumentException("This ain't mine", "fe");

            FileEntryImpl fei = (FileEntryImpl)fe;
            try {
                if (fei.IsFolder)
                    Directory.Delete(fei.RealPath, true);
                else
                    File.Delete(fei.RealPath);
            } catch (IOException ex) {
                if (!ignoreErrors)
                    throw ex;
            }
            XmlDirInfo.Delete(fe.Guid);
        }

        public void Rename(IFileEntry fe, string NewName) {
            if (string.IsNullOrEmpty(NewName))
                throw new ArgumentException("Parameter cannot be null or empty", "NewName");

            if (NewName.Contains("/"))
                throw new ArgumentException("Name cannot contain slash character", "NewName");

            if (fe is IFile) {
                XmlDirInfo.AssertCanRename(fe.Guid, NewName);
                File.Move(Path.Combine(this.RealPath, fe.Name), Path.Combine(this.RealPath, NewName));
                XmlDirInfo.Rename(fe.Guid, NewName);
            } else {
                Directory.Move(Path.Combine(this.RealPath, fe.Name), Path.Combine(this.RealPath, NewName));
            }
        }

        public long GetTotalSize() {
            return GetTotalSizeResursice(this);
        }

        #endregion

        private long GetTotalSizeResursice(IFolder folder) {
            long result = 0;
            foreach (IFolder x in folder.GetFolders())
                result += GetTotalSizeResursice(x);
            foreach (IFile f in folder.GetFiles())
                result += f.Size;
            return result;
        }

        private XmlDirInfo xmlDirInfo;
        internal XmlDirInfo XmlDirInfo {
            get {
                if (xmlDirInfo == null)
                    xmlDirInfo = new XmlDirInfo(Path.Combine(this.RealPath, ".dirinfo.xml"));
                return xmlDirInfo;
            }
        }
    }

}

