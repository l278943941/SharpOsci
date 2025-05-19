cmake_minimum_required(VERSION 3.10)

include(FetchContent)


project(glfwRender VERSION 0.1.0 LANGUAGES CXX)




find_package(glfw3 3.4 quiet)
if(NOT glfw3_FOUND)



endif

