# Ogooreck

Sneaky Test library

## API Testing

### Get

<!-- snippet: ApiGetSample -->
<a id='snippet-apigetsample'></a>
```cs
[Fact]
public Task GetProducts() =>
    API.Given(URI("/api/products"))
        .When(GET)
        .Then(OK);
```
<sup><a href='/src/Ogooreck.Sample.Api.Tests/ApiTests.cs#L12-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-apigetsample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Post

<!-- snippet: ApiPostSample -->
<a id='snippet-apipostsample'></a>
```cs
[Fact]
public Task RegisterProduct() =>
    API.Given(
            URI("/api/products"),
            BODY(new RegisterProductRequest("abc-123", "Ogooreck"))
        )
        .When(POST)
        .Then(CREATED());
```
<sup><a href='/src/Ogooreck.Sample.Api.Tests/ApiTests.cs#L23-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-apipostsample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
