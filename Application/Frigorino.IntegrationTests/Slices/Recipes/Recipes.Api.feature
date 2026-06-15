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

  Scenario: Creating a recipe seeds one default section
    Given there is a recipe named "Pizza"
    When I GET the sections of recipe "Pizza" via the API
    Then the API response status is 200
    And the API sections of recipe "Pizza" number 1

  Scenario: Adding a second section appends it after the first
    Given there is a recipe named "Pizza"
    When I POST a section named "Dough" to recipe "Pizza" via the API
    Then the API response status is 201
    When I GET the sections of recipe "Pizza" via the API
    Then the API sections of recipe "Pizza" number 2

  Scenario: Deleting the only section is rejected
    Given there is a recipe named "Pizza"
    When I DELETE the only section of recipe "Pizza" via the API
    Then the API response status is 400

  Scenario: Deleting a non-empty section cascades its items, and restore brings them back
    Given there is a recipe named "Pizza"
    And I POST a section named "Topping" to recipe "Pizza" via the API
    And the recipe "Pizza" has an item "Cheese" in section "Topping"
    When I DELETE the section "Topping" of recipe "Pizza" via the API
    Then the API response status is 204
    And the API response when getting items of recipe "Pizza" omits "Cheese"
    When I POST restore for the section "Topping" of recipe "Pizza" via the API
    Then the API response status is 200
    And the API response when getting items of recipe "Pizza" includes "Cheese"

  Scenario: Items live in the section they were added to
    Given there is a recipe named "Pizza"
    And I POST a section named "Topping" to recipe "Pizza" via the API
    And the recipe "Pizza" has an item "Cheese" in section "Topping"
    When I GET the items of recipe "Pizza" via the API
    Then the recipe item "Cheese" is in section "Topping"

  Scenario: A section change moves the recipe revision token
    Given there is a recipe named "Pizza"
    When I capture the revision of recipe "Pizza" via the API
    And I POST a section named "Dough" to recipe "Pizza" via the API
    And I capture the revision of recipe "Pizza" via the API
    Then the two captured recipe revisions differ

  Scenario: A new recipe has no source links
    Given there is a recipe named "Pizza"
    When I GET the source links of recipe "Pizza" via the API
    Then the API response status is 200
    And the API source links of recipe "Pizza" number 0

  Scenario: Adding a valid source link succeeds and is listed
    Given there is a recipe named "Pizza"
    When I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    Then the API response status is 201
    And the API source links of recipe "Pizza" number 1

  Scenario: Adding a non-http source link returns a validation error
    Given there is a recipe named "Pizza"
    When I POST a source link "ftp://example.com/file" with no scheme to recipe "Pizza" via the API
    Then the API response status is 400
    And the API response has a validation error for "Url"

  Scenario: Deleting a source link removes it, and restore brings it back
    Given there is a recipe named "Pizza"
    And I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    When I DELETE the source link "Best Pizza" of recipe "Pizza" via the API
    Then the API response status is 204
    And the API source links of recipe "Pizza" number 0
    When I POST restore for the source link "Best Pizza" of recipe "Pizza" via the API
    Then the API response status is 200
    And the API source links of recipe "Pizza" number 1

  Scenario: A source-link change moves the recipe revision token
    Given there is a recipe named "Pizza"
    When I capture the revision of recipe "Pizza" via the API
    And I POST a source link "https://example.com/pizza" labelled "Best Pizza" to recipe "Pizza" via the API
    And I capture the revision of recipe "Pizza" via the API
    Then the two captured recipe revisions differ
