# smallapi
oh cool my first http api

CRUD with a small sqlite database on books and authors (or rather just CR)

# installation
eh this is just a tiny vs studio thing, build from there idk

# functionality
GET authors/{id} shows an author by id along with all their books    
POST authors/{name} creates a new author with specified name    
POST authors/{id}/{bookname} creates a new book under a specified author

non-numerical data in {id} returns 400, nonexistent author ids return 404, trying to create an already existing author/book returns 403
