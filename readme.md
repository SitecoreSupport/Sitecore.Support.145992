# Sitecore.Support.158988
formerly known as Sitecore.Support.145992

The following issues have been resolved:

## 8.2.2.0
* 145992 Fields which store DateTime, Date, Boolean, ID content are indexed incorrectly.
* 146322 CloudSearchDocumentBuilder is not taken from documentBuilderType node of index configuration.
* 146415 NotSupportedException is thrown during indexing __semantics fields.

## 8.2.2.1:
* 147541 Resulting native query is logged partially and in DEBUG level.
* 147549 Count, Any, First, Single, ElementAt retrives exsessive documents.
* 147550 Search result contains 50 items by default.
* 150080 Take, Skip, Page methods don't work.
* 150103 Linq Provider parses a service response 4 times.

## 8.2.2.2:
* 151246 Ampersand (&) can cause AzureSearchServiceRESTCallException exceptions.

## 8.2.2.3:
* 157546 Azure Search provider does not support full-text search.

## 8.2.2.4:
* 155653 Collection field types (multilist, treelist and so on) contain old values when got cleared.
* introduced an issue that was fixed in the 8.2.2.7 version of the patch

## 8.2.2.5:
* 164633 The CloudSearchUpdateContext.Delete(IIndexableId) fails with an exception in case the search index does not contain any matching documents.

## 8.2.2.6:
* 166765 Incorrect grouping of search clauses.

## 8.2.2.7:
* fix an issue that was introduced in 8.2.2.4

## 8.2.2.8
* 94953 (226988) standard values are not indexed when IndexAllFields is set to false
* 170254 wrong system type mapping for some Sitecore field types

## 8.2.2.9
* 162451 Search queries may use wrong syntax ignoring Azure Search schema settings

## License  

This patch is licensed under the [Sitecore Corporation A/S License for GitHub](https://github.com/sitecoresupport/Sitecore.Support.145992/blob/master/LICENSE).  

## Download  

Downloads are available via [GitHub Releases](https://github.com/sitecoresupport/Sitecore.Support.145992/releases).  

[![Total downloads](https://img.shields.io/github/downloads/SitecoreSupport/Sitecore.Support.145992/total.svg)](https://github.com/SitecoreSupport/Sitecore.Support.145992/releases)
