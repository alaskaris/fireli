# FiReLi
A generic File Repository Library that deals with the annoying details of stroring and retrieving files in a uniform way from different types of sources like file system, relational databases etc.

Borrowing heavily from Azure Blob Storage API, the following features are planned:
- queuing file operations to avoid locking etc.
- return meaningful messages about each operation execution, following the motif of ReST
- delete operation is soft and marks the file for deletion
- garbage collecting for the deleted files
- support for arbitrary metadata

