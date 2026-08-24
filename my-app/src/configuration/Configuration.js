const Configuration = {
    // Auth endpoints
    SignIn: '/Auth/signin',
    SignUp: '/Auth/signup',
    
    // Basket endpoints
    GetBasket: '/Baskets/user',
    AddToBasket: '/Baskets',
    UpdateBasket: '/Baskets',
    DeleteFromBasket: '/Baskets',
    ClearBasket: '/Baskets/clear/user',
    
    // User endpoints
    GetUser: '/Users',
    UpdateUser: '/Users',
    ChangePassword: '/Users/password',
    
    // Product endpoints
    GetProducts: '/Productss',
    GetProduct: '/Productss',
    SearchProducts: '/products/search',
    
    // Category endpoints
    GetCategories: '/categories/with-products',
    
    // Order endpoints
    CreateOrder: '/Orders',
    GetUserOrders: '/Users/purchases'
};

export default Configuration;