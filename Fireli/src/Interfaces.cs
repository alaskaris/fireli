using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Fireli
{
    public interface IRepMan
    {
        IRepository Acquire(string name);
        bool HasRepository(string name);
        bool QuotasEnabled { get; set;}
        long QuotasLimit { get;set;}
    }

    public interface IFileEntry
    {
        Guid Guid { get;}
        bool IsFolder { get;}
        long Size { get;}
        string Name { get;}
        string OriginalFilename { get;}

        /// <summary>
        /// το path μαζί με το όνομα του repository
        /// </summary>
        string FullPath { get;}

        /// <summary>
        /// το path μέσα στο repository
        /// </summary>
        string VirtualPath { get;}

        IFolder Parent { get;}

        IRepository Repository { get;}

        /// <summary>
        /// Διαγράφει το συγκεκριμένο αρχείο
        /// </summary>
        /// <param name="ignoreErrors">να αγνοηθούν τυχόν σφάλματα</param>
        void Delete(bool ignoreErrors);

        /// <summary>
        /// Μετονομάζει το συγκεκριμένο αρχείο
        /// </summary>
        /// <param name="NewName"></param>
        void Rename(string NewName);
    }

    public interface IFile : IFileEntry
    {
        Stream GetInputStream();
    }

    /// <summary>
    /// How to handle file adding when the file name already exists
    /// </summary>
    public enum AddFileMode
    {
        Overwrite,
        Throw,
        ChangeName
    }

    public interface IFolder : IFileEntry
    {
        IList<IFile> GetFiles();
        IList<IFolder> GetFolders();
        Guid AddFile(string Name, Stream inputStream, AddFileMode addFileMode);
        void MkDir(string Name);
        IFile GetBy(Guid guid, bool recursive);

        /// <summary>
        /// Διαγράφει το συγκεκριμένο αρχείο
        /// </summary>
        /// <param name="file">το αρχείο που θα διαγραφεί</param>
        /// <param name="ignoreErrors">να αγνοηθούν τυχόν σφάλματα</param>
        void Delete(IFileEntry file, bool ignoreErrors);

        void Rename(IFileEntry file, string NewName);

        IFileEntry GetBy(string VirtualPath);

        long GetTotalSize();
    }

    public interface IRepository : IFolder
    {
        Guid Import(IFile file);
        bool QuotasCustomSettings { get;set;}
        bool QuotasEnabled { get; set;}
        long QuotasLimit { get;set;}
        long GetEffectiveQuotasLimit();
    }
}
