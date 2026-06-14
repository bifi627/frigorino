Feature: Recipes API

  Background:
    Given I am logged in with an active household

  Scenario: Creating a recipe with an empty name returns a validation error
    When I POST a recipe with an empty name via the API
    Then the API response status is 400
    And the API response has a validation error for "Name"

  Scenario: Servings round-trips through create and update
    When I POST a recipe named "Banana Bread" with servings 4 via the API
    Then the API response status is 201
    And the API recipe response has servings 4
    When I PUT recipe "Banana Bread" with servings 6 via the API
    Then the API response status is 200
    And the API recipe response has servings 6

  Scenario: Creating a recipe with out-of-range servings returns a validation error
    When I POST a recipe named "Bad" with servings 0 via the API
    Then the API response status is 400
    And the API response has a validation error for "Servings"

  Scenario: Creating a recipe item with empty text returns a validation error
    Given there is a recipe named "Pasta Carbonara"
    When I POST a recipe item with empty text to "Pasta Carbonara" via the API
    Then the API response status is 400
    And the API response has a validation error for "Text"

  Scenario: Non-member cannot read a household's recipes
    Given I am logged in as "alice"
    And an existing household "Other" owned by "bob" that I am not a member of
    When I GET the recipes of that household via the API
    Then the API response status is 404

  Scenario: Non-creator Member cannot delete a recipe
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "member"
    And "bob" has created a recipe named "BobsRecipe"
    When I DELETE the recipe "BobsRecipe" via the API
    Then the API response status is 403

  Scenario: Non-creator Admin can delete a recipe
    Given I am logged in as "alice"
    And an existing household "Family" owned by "bob" with me as a "admin"
    And "bob" has created a recipe named "BobsRecipe"
    When I DELETE the recipe "BobsRecipe" via the API
    Then the API response status is 204

  Scenario: Adding an item changes the recipe revision token
    Given there is a recipe named "Pasta Carbonara"
    When I capture the revision of recipe "Pasta Carbonara" via the API
    And I POST a recipe item with text "Eggs" to "Pasta Carbonara" via the API
    And I capture the revision of recipe "Pasta Carbonara" via the API
    Then the two captured recipe revisions differ

  Scenario: Reordering a recipe item to the top succeeds
    Given there is a recipe named "Pasta Carbonara"
    And the recipe "Pasta Carbonara" has an item "Eggs"
    And the recipe "Pasta Carbonara" has an item "Bacon"
    When I PATCH "Bacon" to the top of recipe "Pasta Carbonara" via the API
    Then the API response status is 200
    And the API items of recipe "Pasta Carbonara" appear in order: "Bacon, Eggs"

  Scenario: Restoring a soft-deleted recipe item brings it back
    Given there is a recipe named "Pasta Carbonara"
    And the recipe "Pasta Carbonara" has an item "Eggs"
    When I DELETE the recipe item "Eggs" in "Pasta Carbonara" via the API
    Then the API response when getting items of recipe "Pasta Carbonara" omits "Eggs"
    When I POST restore for the recipe item "Eggs" in recipe "Pasta Carbonara" via the API
    Then the API response status is 200
    And the API response when getting items of recipe "Pasta Carbonara" includes "Eggs"

  Scenario: Adding "20 apples" extracts the count but never classifies into Products
    Given there is a recipe named "Pasta Carbonara"
    When I POST a recipe item with text "20 apples" to "Pasta Carbonara" via the API
    Then the recipe item eventually has text "apples" with quantity 20 unit 4
    And the Products table is empty
