using System;
using System.Collections.Generic;
using System.Text;
using Fireli;
using System.Data.SqlClient;
using System.Configuration;

namespace Fireli.DBImpl
{
    public class DBRepMan : IRepMan
    {
        protected SqlConnection GetConnection()
        {
            SqlConnection result = new SqlConnection(ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString);
            result.Open();
            return result;
        }

        #region IRepMan Members

        public IRepository Acquire(string name)
        {
            return new DBRepository(name);
        }

        public bool HasRepository(string name)
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM DBFiles WHERE FileRepository=@name", conn))
                {
                    cmd.Parameters.Add("name", System.Data.SqlDbType.NVarChar, 255).Value = name;
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        private bool quotasEnabled;

        public bool QuotasEnabled
        {
            get { return quotasEnabled; }
            set { quotasEnabled = value; }
        }

        private long quotasLimit;

        public long QuotasLimit
        {
            get { return quotasLimit; }
            set { quotasLimit = value; }
        }

        #endregion
    }

    public class DBRepository : DBFolder, IRepository
    {
        public DBRepository(string name)
            : base(Guid.Empty, name, name, null)
        {
            this.repository = this;
        }

        #region IRepository Members

        public Guid Import(IFile file)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public bool QuotasCustomSettings
        {
            get
            {
                return false;
            }
            set { }
        }

        public bool QuotasEnabled
        {
            get
            {
                return false;
            }
            set
            {
            }
        }

        public long QuotasLimit
        {
            get
            {
                return 0L;
            }
            set
            {
            }
        }

        public long GetEffectiveQuotasLimit()
        {
            return 0L;
        }

        #endregion

        public override string VirtualPath
        {
            get
            {
                return "/";
            }
        }

        public override IFolder Parent
        {
            get
            {
                return null;
            }
        }

    }

    public class DBFile : DBFileEntry, IFile
    {
        public DBFile(Guid guid, long size, string name, string originalName, DBRepository repository)
            : base(guid, size, name, originalName, repository)
        {
        }

        #region IFile Members

        public System.IO.Stream GetInputStream()
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("SELECT FileData FROM DBFiles WHERE FileRepository=@fileRepository AND FileID=@fileID", conn))
                {
                    cmd.Parameters.Add("fileRepository", System.Data.SqlDbType.NVarChar, 255).Value = repository.Name;
                    cmd.Parameters.Add("fileID", System.Data.SqlDbType.UniqueIdentifier).Value = Guid;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            long len = dr.GetBytes(0, 0, null, 0, 0);
                            byte[] bt = new byte[len];
                            dr.GetBytes(0, 0, bt, 0, (int)len);
                            return new System.IO.MemoryStream(bt);
                        }
                        else
                            return null;
                    }
                }
            }
        }

        #endregion
    }

    public class DBFolder : DBFileEntry, IFolder
    {
        public DBFolder(Guid guid, string name, string originalName, DBRepository repository)
            : base(guid, -1, name, originalName, repository)
        {
        }
        
        private long GetTotalSizeResursice(IFolder folder)
        {
            long result = 0;
            foreach (IFolder x in folder.GetFolders())
                result += GetTotalSizeResursice(x);
            foreach (IFile f in folder.GetFiles())
                result += f.Size;
            return result;
        }

        #region IFolder Members

        public IList<IFile> GetFiles()
        {
            List<IFile> result = new List<IFile>();
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("SELECT FileID, FileName, FileOriginalName, FileSize FROM DBFiles WHERE FileRepository=@fileRepository", conn))
                {
                    cmd.Parameters.Add("fileRepository", System.Data.SqlDbType.NVarChar, 255).Value = repository.Name;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                            result.Add(new DBFile(
                                dr.GetGuid(0),
                                dr.GetInt32(3),
                                dr.GetString(1),
                                dr.GetString(2),
                                repository));
                    }
                }
            }
            return result;
        }

        public IList<IFolder> GetFolders()
        {
            return new List<IFolder>();
        }

        protected Guid InsertFile(string Name, string OriginalName, System.IO.Stream inputStream)
        {
            int len = (int)inputStream.Length;
            byte[] bt = new byte[len];
            inputStream.Read(bt, 0, len);

            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("INSERT INTO DBFiles (FileRepository, FileName, FileOriginalName, FileSize, FileData) VALUES (@fileRepository, @fileName, @fileOriginalName, @fileSize, @fileData)", conn))
                {
                    cmd.Parameters.Add("fileRepository", System.Data.SqlDbType.NVarChar, 255).Value = repository.Name;
                    cmd.Parameters.Add("fileName", System.Data.SqlDbType.NVarChar, 255).Value = Name;
                    cmd.Parameters.Add("fileOriginalName", System.Data.SqlDbType.NVarChar, 255).Value = OriginalName;
                    cmd.Parameters.Add("fileSize", System.Data.SqlDbType.Int).Value = len;
                    cmd.Parameters.AddWithValue("fileData", bt);

                    cmd.ExecuteNonQuery();
                }
            }

            return GetBy(Name).Guid; // second query...
        }

        protected void UpdateFile(Guid guid, System.IO.Stream inputStream)
        {
            int len = (int)inputStream.Length;
            byte[] bt = new byte[len];
            inputStream.Read(bt, 0, len);

            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("UPDATE DBFiles SET FileData=@fileData WHERE FileID=@fileID", conn))
                {
                    cmd.Parameters.AddWithValue("fileData", bt);
                    cmd.Parameters.Add("fileID", System.Data.SqlDbType.UniqueIdentifier).Value = guid;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Guid AddFile(string Name, System.IO.Stream inputStream, AddFileMode addFileMode)
        {
            IFileEntry existing = GetBy(Name);
            string OriginalName;
            if (existing != null)
                OriginalName = existing.OriginalFilename;
            else
                OriginalName = Name;

            if (addFileMode == AddFileMode.Throw && existing != null)
                throw new Exception("File already exists");
            if (addFileMode == AddFileMode.Overwrite && existing != null)
            {
                UpdateFile(existing.Guid, inputStream);
                return existing.Guid;
            }
            else
                return InsertFile(Name, OriginalName, inputStream);
        }

        public void MkDir(string Name)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public IFile GetBy(Guid guid, bool recursive)
        {
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("SELECT FileID, FileName, FileOriginalName, FileSize FROM DBFiles WHERE FileRepository=@fileRepository AND FileID=@fileID", conn))
                {
                    cmd.Parameters.Add("fileRepository", System.Data.SqlDbType.NVarChar, 255).Value = repository.Name;
                    cmd.Parameters.Add("fileID", System.Data.SqlDbType.UniqueIdentifier).Value = guid;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                            return new DBFile(
                                dr.GetGuid(0),
                                dr.GetInt32(3),
                                dr.GetString(1),
                                dr.GetString(2),
                                repository);
                        else
                            return null;
                    }
                }
            }
        }

        public void Delete(IFileEntry file, bool ignoreErrors)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Rename(IFileEntry file, string NewName)
        {
            throw new Exception("E_NIMPL");
        }

        public IFileEntry GetBy(string VirtualPath)
        {
            if (string.IsNullOrEmpty(VirtualPath))
                return repository;
            using (SqlConnection conn = GetConnection())
            {
                using (SqlCommand cmd = new SqlCommand("SELECT FileID, FileName, FileOriginalName, FileSize FROM DBFiles WHERE FileRepository=@fileRepository AND FileName=@fileName", conn))
                {
                    cmd.Parameters.Add("fileRepository", System.Data.SqlDbType.NVarChar, 255).Value = repository.Name;
                    cmd.Parameters.Add("fileName", System.Data.SqlDbType.NVarChar, 255).Value = VirtualPath;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                            return new DBFile(
                                dr.GetGuid(0),
                                dr.GetInt32(3),
                                dr.GetString(1),
                                dr.GetString(2),
                                repository);
                        else
                            return null;
                    }
                }
            }
        }

        public long GetTotalSize()
        {
            return GetTotalSizeResursice(this);
        }
        #endregion
    }

    public class DBFileEntry : IFileEntry
    {
        private Guid guid;
        private long size;
        private string name;
        private string originalName;
        protected DBRepository repository;

        protected SqlConnection GetConnection()
        {
            SqlConnection result = new SqlConnection(ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString);
            result.Open();
            return result;
        }

        public DBFileEntry(Guid guid, long size, string name, string originalName, DBRepository repository)
        {
            this.guid = guid;
            this.size = size;
            this.name = name;
            this.originalName = originalName;
            this.repository = repository;
        }

        #region IFileEntry Members

        public Guid Guid
        {
            get { return guid; }
        }

        public bool IsFolder
        {
            get { return this is IFolder; }
        }

        public long Size
        {
            get { return size; }
        }

        public string Name
        {
            get { return name; }
        }

        public string OriginalFilename
        {
            get { return originalName; }
        }

        public string FullPath
        {
            get { return repository.Name + VirtualPath; }
        }

        public virtual string VirtualPath
        {
            get { return "/" + name; }
        }

        public virtual IFolder Parent
        {
            get { return repository; }
        }

        public IRepository Repository
        {
            get { return repository; }
        }

        public void Delete(bool ignoreErrors)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Rename(string NewName)
        {
            throw new Exception("Operation not supported yet");
        }

        #endregion
    }

}
