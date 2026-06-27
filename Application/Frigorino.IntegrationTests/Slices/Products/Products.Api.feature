Feature: Products catalog API

  Background:
    Given I am logged in with an active household

  Scenario: Owner overrides a product to a longer shelf life
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "DairyAndEggs" expiry "AiRecommendsShelfLife" shelf life 14
    Then the API response status is 200
    And the product API response is overridden
    And the product API response effective shelf life is 14

  Scenario: Overriding to non-perishable drops the shelf life
    Given a classified product "salt" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 200
    And the product API response effective expiry is "NonPerishable"
    And the product API response has no effective shelf life

  Scenario: Reset restores the AI verdict
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 200
    And the product API response is overridden
    When I DELETE the product override
    Then the API response status is 200
    And the product API response is not overridden
    And the product API response effective expiry is "AiRecommendsShelfLife"
    And the product API response effective shelf life is 7

  Scenario: Shelf life out of bounds is rejected
    Given a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "DairyAndEggs" expiry "AiRecommendsShelfLife" shelf life 0
    Then the API response status is 400
    And the API response has a validation error for "ShelfLifeDays"

  Scenario: Owner deletes a product
    Given a classified product "milk" with AI shelf life 7
    When I DELETE the product entirely
    Then the API response status is 204
    When I GET the product catalog via the API
    Then the API response status is 200
    And the product catalog API response is empty

  Scenario: Member cannot override a product
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And a classified product "milk" with AI shelf life 7
    When I PUT a product override with category "Pantry" expiry "NonPerishable" and no shelf life
    Then the API response status is 403

  Scenario: Member cannot delete a product
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And a classified product "milk" with AI shelf life 7
    When I DELETE the product entirely
    Then the API response status is 403

  Scenario: Non-member cannot read the catalog
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the product catalog via the API
    Then the API response status is 404
