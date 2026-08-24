import AxiosServices from "./AxiosServices";
import Configuration from "../configuration/Configuration"; 

const axiosService = new AxiosServices()

export default class AuthServices {
    async SignUp(data) {
        console.log('SignUp - Configuration.SignUp:', Configuration.SignUp);
        console.log('SignUp - data:', data);
        return axiosService.post(Configuration.SignUp, data);
    }

    async SignIn(data) {
        console.log('SignIn - Configuration.SignIn:', Configuration.SignIn);
        console.log('SignIn - data:', data);
        return axiosService.post(Configuration.SignIn, data);
    }
}