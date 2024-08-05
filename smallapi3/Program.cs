using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using Microsoft.Data.Sqlite;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.Eventing.Reader;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Xml.Linq;
using System.Net.Http;
//using smallapi3;



var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();





bool checkAuthor(string author, SqliteCommand command)
{
	command.CommandText = "SELECT COUNT(*) FROM Authors WHERE Name = @newName"; //check if author already exists (from name)
	command.Parameters.AddWithValue("@newName", author);
	var authorCount = Convert.ToInt32(command.ExecuteScalar());
	return (authorCount != 0);
}

bool checkBook(string bookname, SqliteCommand command)
{
	command.CommandText = "SELECT COUNT(*) FROM Books WHERE name = @bookname"; //check if already exists (from name)
	command.Parameters.AddWithValue("@bookname", bookname);
	var bookCount = Convert.ToInt32(command.ExecuteScalar());
	return (bookCount != 0);
}

string getAuthorName(int id, SqliteCommand command)
{
	command.CommandText = "SELECT Name FROM Authors WHERE id = @id"; //get name of author, check existence
	command.Parameters.AddWithValue("@id", id);
	using var reader = command.ExecuteReader();
	if (reader.HasRows)
	{
		while (reader.Read())
		{
			return reader.GetString(0);
		}
	}
	return null;
}

string getBookName(int id, SqliteCommand command)
{
	command.CommandText = "SELECT name FROM Books WHERE id = @id"; //get name of book, check existence
	command.Parameters.AddWithValue("@id", id);
	using var reader = command.ExecuteReader();
	if (reader.HasRows)
	{
		while (reader.Read())
		{
			return reader.GetString(0);
		}
	}
	return null;
}



var authorsApi = app.MapGroup("/author");

authorsApi.MapGet("/{idstr}", async (HttpContext httpContext, string idstr) =>		//GET authors/id
{

	int id;
	var booklist = new List<Book>();
	Author author = null;
	if (!int.TryParse(idstr, out id)) {
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid author ID"); ;
	};	
	var getauthorsql = "SELECT Authors.id, Authors.name FROM Authors WHERE Authors.id = " + " " + id;	//parsing takes care of unsafe strings anyway
	var getbooksql = "SELECT Books.id, Books.name FROM Books INNER JOIN Authors ON Authors.id = Books.author WHERE Authors.id = " + " " + id;
	

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();

		using var commandAuth = new SqliteCommand(getauthorsql, connection);
		using var commandBook = new SqliteCommand(getbooksql, connection);

		using var readerBook = commandBook.ExecuteReader();
		if (readerBook.HasRows)
		{
			while (readerBook.Read())
			{
				var idInner = readerBook.GetInt32(0);
				var Name = readerBook.GetString(1);
				Console.WriteLine($"{id} {idInner} - {Name}");
				booklist.Add(new Book(idInner, Name));
			}
		}
		else
		{
			Console.WriteLine("No books found.");
		}

		using var readerAuth = commandAuth.ExecuteReader();
		if (readerAuth.HasRows)
		{
			while (readerAuth.Read())
			{
				var idInner = readerAuth.GetInt32(0);
				var Name = readerAuth.GetString(1);
				Console.WriteLine($"{id} {idInner} - {Name}");
				author = new Author(idInner, Name, booklist);
			}
		}
		else
		{
			Console.WriteLine("No authors found.");
		}

		connection.Close();

	}
	catch (SqliteException ex)
	{
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message);    //unlikely this would pop up
																				
	}

	//return JsonConvert.SerializeObject(sampleAuthors);
	//return sampleAuthors;

	if (author == null)
	{
		httpContext.Response.StatusCode = StatusCodes.Status404NotFound; // Set the status code to 400
		return new Response(404, "Not found", null, "No author with such ID"); ;
	}

	else return new Response(200, "Success", author, null);


}
);
authorsApi.MapPost("/{name}", async (HttpContext httpContext, string name) =>
	{
		try
		{
			using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
			connection.Open();
			SqliteCommand command = new SqliteCommand();
			command.Connection = connection;


			if (checkAuthor(name, command)) {		//checks if author exists, returns 403 if true
				httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
				return new Response(403, "Forbidden", null, $"The author with the name '{name}' already exists");
			}

			command.CommandText = "INSERT INTO Authors(Name) VALUES (@newName)";
			var rowInserted = command.ExecuteNonQuery();	//insert new author into the thing
			Console.WriteLine($"The author '{name}' has been created successfully.");
			httpContext.Response.StatusCode = StatusCodes.Status201Created;
			return new Response(201, "Created", null, $"The author '{name}' has been created successfully.");
		}
		catch (SqliteException ex)
		{
			Console.WriteLine(ex.Message);
			httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
			return new Response(500, "Internal server error", null, ex.Message); //most often if the db is open with another program
		}
		
	}
);
authorsApi.MapPost("/{idstr}/{bookname}", async (HttpContext httpContext, string idstr, string bookname) =>
	{
		int id;
		if (!int.TryParse(idstr, out id))
		{
			httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
			return new Response(400, "Bad request", null, "Invalid author ID"); ;
		};
		
		try
		{
			using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
			connection.Open();
			SqliteCommand command = new SqliteCommand();
			command.Connection = connection;

			string authorname = getAuthorName(id, command);  //gets author name from string
			if (authorname == null)
			{
				httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
				return new Response(404, "Not found", null, "No author with such ID"); ;
			}

			if (checkBook(bookname, command))
			{      
				httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
				return new Response(403, "Forbidden", null, $"The book with the name '{bookname}' already exists");
			}

			command.CommandText = "INSERT INTO Books(name, author) VALUES (@bookname, @id)";
			var rowInserted = command.ExecuteNonQuery();

			Console.WriteLine($"The book '{bookname}' by {authorname} has been created successfully.");
			httpContext.Response.StatusCode = StatusCodes.Status201Created;
			connection.Close();
			return new Response(201, "Created", null, $"The book '{bookname}' by {authorname} has been created successfully.");

			

			

		}
		catch (SqliteException ex)
		{
			httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
			return new Response(500, "Internal server error", null, ex.Message);

		}

	}
);
authorsApi.MapDelete("/{idstr}", async (HttpContext httpContext, string idstr) =>	//delete author
{
	int id;
	if (!int.TryParse(idstr, out id))
	{
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid author ID"); ;
	};

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();
		SqliteCommand command = new SqliteCommand();
		command.Connection = connection;


		string authorname = getAuthorName(id, command);  //gets author name from string
		if (authorname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No author with such ID"); ;
		}

		command.CommandText = "DELETE FROM Authors WHERE id = @id";
		var rowInserted = command.ExecuteNonQuery();    //insert new author into the thing
		Console.WriteLine($"The author '{authorname}' with id '{id}' has been successfully deleted.");
		httpContext.Response.StatusCode = StatusCodes.Status201Created;
		return new Response(204, "No content", null, $"The author '{authorname}' with id '{id}' has been successfully deleted.");
	}
	catch (SqliteException ex)
	{
		Console.WriteLine(ex.Message);
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message + " \n right now only deleting of bookless authors is possible \n TODO: proper cascading in db");
	}

}
);
authorsApi.MapPut("/{idstr}/{newname}", async (HttpContext httpContext, string idstr, string newname) => //update author (change name)
{
	int id;
	if (!int.TryParse(idstr, out id))
	{
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid author ID"); ;
	};

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();
		SqliteCommand command = new SqliteCommand();
		command.Connection = connection;

		string authorname = getAuthorName(id, command);  //checks if author with id exists
		if (authorname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No author with such ID"); ;
		}

		if (checkAuthor(newname, command))
		{
			httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
			return new Response(403, "Forbidden", null, $"The author with the name '{newname}' already exists");
		}


		command.CommandText = "UPDATE Authors SET Name = @newName WHERE id = @id";
		var rowInserted = command.ExecuteNonQuery();


		httpContext.Response.StatusCode = StatusCodes.Status200OK;
		connection.Close();
		return new Response(200, "OK", null, $"Author with id {id} name updated to {newname} successfully");





	}
	catch (Exception ex)
	{
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message);

	}

}
);

var bookApi = app.MapGroup("/book");
bookApi.MapDelete("/{idstr}", async (HttpContext httpContext, string idstr) =>   //delete book
{
	int id;
	if (!int.TryParse(idstr, out id))
	{
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid book ID"); ;
	};

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();
		SqliteCommand command = new SqliteCommand();
		command.Connection = connection;


		string bookname = getBookName(id, command);  //gets author name from string
		if (bookname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No author with such ID"); ;
		}

		command.CommandText = "DELETE FROM Books WHERE id = @id";
		var rowInserted = command.ExecuteNonQuery();    //insert new author into the thing
		Console.WriteLine($"The book '{bookname}' with id '{id}' has been successfully deleted.");
		httpContext.Response.StatusCode = StatusCodes.Status201Created;
		return new Response(204, "No content", null, $"The author '{bookname}' with id '{id}' has been successfully deleted.");
	}
	catch (SqliteException ex)
	{
		Console.WriteLine(ex.Message);
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message + " \n right now only deleting of bookless authors is possible \n TODO: proper cascading in db");
	}

}
);
bookApi.MapPut("/{idstr}/{newname}", async (HttpContext httpContext, string idstr, string newname) => //update book (change name) boilerplate yawn
{
	int id;
	if (!int.TryParse(idstr, out id))
	{
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid book ID"); ;
	};

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();
		SqliteCommand command = new SqliteCommand();
		command.Connection = connection;

		string bookname = getBookName(id, command);  //checks if book with id exists
		if (bookname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No book with such ID"); ;
		}

		if (checkBook(newname, command))
		{
			httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
			return new Response(403, "Forbidden", null, $"The book with the name '{newname}' already exists");
		}


		command.CommandText = "UPDATE Books SET name = @bookname WHERE id = @id";
		var rowInserted = command.ExecuteNonQuery();


		httpContext.Response.StatusCode = StatusCodes.Status200OK;
		connection.Close();
		return new Response(200, "OK", null, $"Book with id {id} name updated to {newname} successfully");





	}
	catch (Exception ex)
	{
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message);

	}

}
);

bookApi.MapPut("/move/{idstr}/{newauthoridstr}", async (HttpContext httpContext, string idstr, string newauthoridstr) => //update book (change name) boilerplate yawn
{
	int id;
	int newauthorid;
	if (!int.TryParse(idstr, out id) || !int.TryParse(newauthoridstr, out newauthorid))
	{
		httpContext.Response.StatusCode = StatusCodes.Status400BadRequest; // Set the status code to 400
		return new Response(400, "Bad request", null, "Invalid author and/or book ID"); ;
	};

	try
	{
		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
		connection.Open();
		SqliteCommand command = new SqliteCommand();
		SqliteCommand newcommand = new SqliteCommand(); //hacky conflict resolution
		command.Connection = connection;
		newcommand.Connection = connection;

	string bookname = getBookName(id, command);  //checks if book with id exists
		if (bookname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No book with such ID"); ;
		}

		string authorname = getAuthorName(newauthorid, newcommand);  //gets author name from string
		if (authorname == null)
		{
			httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
			return new Response(404, "Not found", null, "No author with such ID"); ;
		}


		command.CommandText = "UPDATE Books SET author = @newauthorid WHERE id = @id";
		command.Parameters.AddWithValue("@newauthorid", newauthorid);
		//command.Parameters.AddWithValue("@id", id);
		var rowInserted = command.ExecuteNonQuery();


		httpContext.Response.StatusCode = StatusCodes.Status200OK;
		connection.Close();
		return new Response(200, "OK", null, $"Book with id {id} moved to author {newauthorid} {authorname} successfully");





	}
	catch (Exception ex)
	{
		httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
		return new Response(500, "Internal server error", null, ex.Message);

	}

}
);

var testingApi = app.MapGroup("/test");
testingApi.MapGet("/", async (HttpContext httpContext) =>        
{
	using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
	connection.Open();
	SqliteCommand command = new SqliteCommand();
	command.Connection = connection;

	Author testauthor = new Author(5, "test", new List<Book> { new Book(1, "book") });

	string a = "Dril";

	Console.WriteLine(checkAuthor(a,command));


	Console.WriteLine(testauthor.Authorname);
	return new Response(200, "Success", testauthor, null);
	

}
);

testingApi.MapPost("/", async (HttpContext httpContext, HttpRequest request) =>        
{
	var resp = await request.ReadFromJsonAsync<Response>();
	return resp;
}
);

testingApi.MapPut("/", async (HttpContext httpContext, HttpRequest request) =>
{
	string id = "123";
	string newname = "abc";
	httpContext.Response.StatusCode = StatusCodes.Status200OK;
	//connection.Close();
	return new Response(200, "OK", null, $"Author with id {id} name updated to {newname} successfully");

}
);

app.Run();

public record Author(int? Id, string? Authorname, List<Book>? Booklist);
public record Book(int? Id, string? Bookname);
public record Jsonerror(int? code, string? error, string? details);
public record Response(int code, string? status, object? data, string? details);




//[JsonSerializable(typeof(Todo[]))]
//[JsonSerializable(typeof(List<Todo>))]
//[JsonSerializable(typeof(Author[]))]
[JsonSerializable(typeof(List<Author>))]
[JsonSerializable(typeof(Author))]
[JsonSerializable(typeof(Response))]
[JsonSerializable(typeof(List<>))]

internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}

