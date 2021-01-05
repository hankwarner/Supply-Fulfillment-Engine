# README
Azure HTTP functions to provide order sourcing information based on customer shipping information and item inventory.

# Open API Definitions

https://sourcing-engine.azurewebsites.net/api/swagger/ui?code=n3yCngJqMFOHkAMQi8MINBfLbCTpMs0PlbGj1S7TRQfnXNgg5xXGmA==&clientId=apim-sourcing-engine-apim


# CI/CD workflow and Pull Request instructions

1. Create a new branch with a name suitable for the code being added/refactored.

2. Send initial pull request to the **test** branch. Once merged, a build & deploy action in **Debug** configuration will be triggered to the _sourcing-engine-test_ function app. OpenAPI defintion here: https://sourcing-engine-test.azurewebsites.net/api/swagger/ui?code=9pvEciom6lartFkgZsm9Jq6Ro2QR2s9eSvPvXDBx8LpdFOjHLQ88LQ==

3. Once approved in test, the next pull request should be sent to the **staging** branch. Once merged, a build & deploy action in **Release** configuration will be triggered to the _sourcing-microservices_ staging environment (a deployment slot in the production function app). OpenAPI defintion here: https://sourcing-engine-staging.azurewebsites.net/api/swagger/ui?code=lQ61oPal80lcseqFJgaf6XhgDAURJWsy3fZsbJ1enadD4kp1JGimZA==

4. Once approved in staging, the final pull request should be sent to the **master** branch. Once merged, a build & deploy action in **Release** configuration will be triggered to the _sourcing-microservices_ production environment. OpenAPI defintion in section above.


# SourceOrderFromSite
Endpoint for sourcing orders from Ferguson.com. Order sourcing information will appear on a dashboard for Ferguson associates, along with messaging containing specific sourcing details.


## Route
`order/source`


## Body
Accepts a JSON object with the following key-value pairs:


### Required
 **string** `atgOrderId`

 **string** `sellWhse`: Branch ID of the selling warehouse. 

**object** `shipping`
      
   **object** `shipTo`

   **string** `address1`

   **string** `address2` 
   
   **string** `city`
   
   **string** `state` 
   
   **string** `zip` 




## Example Request
```
{
  "taxExempt": "N",
  "atgOrderId": "W639300633",
  "custAccountId": "POLLARDWTR",
  "customerId": "69986",
  "taxAmount": "1.36",
  "customerName": "Thomas Anderson",
  "userEmail": "mr_anderson@neo.com",
  "orderSubmitDate": "2020-07-09 08:16:58",
  "orderEntryDate": "2020-07-09 08:16:58",
  "orderRequiredDate": "2020-07-09",
  "shipFromWhse": "3326",
  "sellWhse": "3326",
  "notes": "Standard",
  "paymentOnAccount": {
    "payment": {
      "cardType": "VI",
      "address1": "1995 Poplar Ridge Road",
      "address2": "",
      "cardholderName": "Thomas Anderson",
      "cardExpirationDate": "2022-08",
      "city": "Aurora",
      "state": "NY",
      "zip": "13026",
      "token": "-E803-2508-3PZ9E1E2YTAF7E",
      "cardMaskedNumber": "XXXXXXXXXXXX2508"
    }
  },
  "shipping": {
    "tax": "0.0",
    "shipViaCode": "OT",
    "price": "0.0",
    "shipTo": {
      "name": "Thomas Anderson",
      "address1": "1995 Poplar Ridge Rd",
      "address2": "",
      "city": "Aurora",
      "country": "US",
      "state": "NY",
      "id": "M",
      "shipInstructionsAttention": "Thomas Anderson",
      "zip": "130269718",
      "shipInstructionsPhoneNumberAreaDialing": "315",
      "shipInstructionsPhoneNumberDialNumber": "7295356"
    }
  },
  "items": [
    {
      "priceFormula": "3-0.000",
      "priceColumn": "135",
      "extendedPrice": "17.0",
      "unitPriceCode": "EA",
      "leadLawFlag": "Y",
      "promotionMultiplier": "1",
      "distributedTax": "1.36",
      "description": "ci8318000259",
      "unitPrice": "17.0",
      "netPrice": "17.0",
      "quantity": "1",
      "masterProdId": "5123328"
    }
  ],
  "sourceSystem": "POL",
  "taxCode": "NY0503"
}
```



## Return
ATG order object containing all information from the initial order, plus the branch ID of the suggested "ship from" location for each item.

```
{
  "shipFrom": "107a",
  "sourcingMessage": "Sourced by line.",
  "processSourcing": true,
  "taxExempt": "N",
  "atgOrderId": "W639300633",
  "custAccountId": "POLLARDWTR",
  "customerId": "69986",
  "taxAmount": "1.36",
  "customerName": "Thomas Anderson",
  "userEmail": "mr_anderson@neo.com",
  "orderSubmitDate": "2020-07-09 08:16:58",
  "orderEntryDate": "2020-07-09 08:16:58",
  "orderRequiredDate": "2020-07-09",
  "shipFromWhse": "3326",
  "sellWhse": "3326",
  "notes": "Standard",
  "sourcingMessage": "",
  "paymentOnAccount": {
    "payment": {
      "cardType": "VI",
      "address1": "1995 Poplar Ridge Road",
      "address2": "",
      "cardholderName": "Thomas Anderson",
      "cardExpirationDate": "2022-08",
      "city": "Aurora",
      "state": "NY",
      "zip": "13026",
      "token": "-E803-2508-3PZ9E1E2YTAF7E",
      "cardMaskedNumber": "XXXXXXXXXXXX2508"
    }
  },
  "shipping": {
    "tax": "0.0",
    "shipViaCode": "OT",
    "price": "0.0",
    "shipTo": {
      "name": "Thomas Anderson",
      "address1": "1995 Poplar Ridge Rd",
      "address2": "",
      "city": "Aurora",
      "country": "US",
      "state": "NY",
      "id": "M",
      "shipInstructionsAttention": "Thomas Anderson",
      "zip": "130269718",
      "shipInstructionsPhoneNumberAreaDialing": "315",
      "shipInstructionsPhoneNumberDialNumber": "7295356"
    }
  },
  "items": [
    {
      "priceFormula": "3-0.000",
      "priceColumn": "135",
      "extendedPrice": "17.0",
      "unitPriceCode": "EA",
      "leadLawFlag": "Y",
      "promotionMultiplier": "1",
      "distributedTax": "1.36",
      "description": "ci8318000259",
      "unitPrice": "17.0",
      "netPrice": "17.0",
      "quantity": "1",
      "masterProdId": "5123328",
      "shipFrom": "533"
    }
  ],
  "sourceSystem": "POL",
  "taxCode": "NY0503"
}
```
