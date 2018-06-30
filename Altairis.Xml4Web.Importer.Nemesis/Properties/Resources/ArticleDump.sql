SELECT
	A.ArticleId,
	A.Title,
	A.Abstract,
	A.CategoryId, C.Name AS CategoryName,
	A.AuthorId, U.Name AS AuthorName, U.Email AS AuthorEmail,
	A.SerialId, S.Name AS SerialName,
	A.DateCreated, A.DatePublished, A.DateUpdated,
	A.AlternateUrl,
	A.PictureData, A.PictureContentType, A.PictureWidth, A.PictureHeight,
	A.Body
FROM
	Articles AS A
	LEFT JOIN Categories AS C ON A.CategoryId = C.CategoryId
	LEFT JOIN Authors AS U ON A.AuthorId = U.AuthorId
	LEFT JOIN Serials AS S ON A.SerialId =  S.SerialId
ORDER BY A.ArticleId
