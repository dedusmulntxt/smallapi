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








var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();







var authorsApi = app.MapGroup("/author");

//authorsApi.MapGet("/", () => sampleAuthors);
//authorsApi.MapGet("/{id}", (int id) =>
//{
//	var sampleAuthors = new List<Author>();
//	var getauthorsql = "SELECT Authors.id, Authors.name, Books.name FROM Books INNER JOIN Authors ON Authors.id = Books.author WHERE Authors.id = " + " " + id;

//	try
//	{
//		using var connection = new SqliteConnection(@"Data Source=Bookstore.db");
//		connection.Open();

//		using var command = new SqliteCommand(getauthorsql, connection);

//		using var reader = command.ExecuteReader();
//		if (reader.HasRows)
//		{
//			while (reader.Read())
//			{
//				var idInner = reader.GetInt32(0);
//				var bookname = reader.GetString(1);
//				var Name = reader.GetString(2);
//				Console.WriteLine($"{id} {bookname} - {Name}");
//				sampleAuthors.Add(new Author(idInner, bookname, Name));
//			}
//		}
//		else
//		{
//			Console.WriteLine("No authors found.");
//		}

//	}
//	catch (SqliteException ex)
//	{
//		Console.WriteLine(ex.Message);
//	}

//	//return JsonConvert.SerializeObject(sampleAuthors);

//	return sampleAuthors;
//}
//);

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
			command.CommandText = "SELECT COUNT(*) FROM Authors WHERE Name = @newName"; //check if already exists
			command.Parameters.AddWithValue("@newName", name);
			var authorCount = Convert.ToInt32(command.ExecuteScalar());
			if (authorCount > 0) {
				httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
				return new Response(403, "Forbidden", null, $"The author with the name '{name}' already exists");
			}
			command.CommandText = "INSERT INTO Authors(Name) VALUES (@newName)";
			var rowInserted = command.ExecuteNonQuery();
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

			command.CommandText = "SELECT Name FROM Authors WHERE id = @id"; //get name of author, check existence
			command.Parameters.AddWithValue("@id", id);
			string authorname = "test";
			using var reader = command.ExecuteReader();
			if (reader.HasRows)
			{
				while (reader.Read())
				{
					authorname = reader.GetString(0);
				}
			}
			else
			{
				Console.WriteLine("No authors found.");
				httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
				return new Response(404, "Not Found", null, $"No author with such id");
			}
			reader.Close();

			command.CommandText = "SELECT COUNT(*) FROM Books WHERE name = @bookname"; //check if book exists
			command.Parameters.AddWithValue("@bookname", bookname);
			var bookCount = Convert.ToInt32(command.ExecuteScalar());
			if (bookCount > 0)
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



app.Run();

public record Author(int? Id, string? Authorname, List<Book>? Booklist);
public record Book(int? Id, string? Bookname);

//public record Jsonerror(int? code, string? error, string? details);

public record Response(int code, string? status, Author? author, string? details);




//[JsonSerializable(typeof(Todo[]))]
//[JsonSerializable(typeof(List<Todo>))]
//[JsonSerializable(typeof(Author[]))]
[JsonSerializable(typeof(List<Author>))]
[JsonSerializable(typeof(Author))]
//[JsonSerializable(typeof(Jsonerror))]
[JsonSerializable(typeof(Response))]

internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}

